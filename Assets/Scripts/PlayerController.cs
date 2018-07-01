﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour {

	public DebugUI DebugUI;
	public Transform BoundsFront;
	public GameObject PlatformPrefab;

	private const string THRUST_AXIS = "Thrust";
	private const string BRAKE_AXIS = "Brake";
	private const string FWD_AXIS = "Vertical";
	private const string ORTHO_AXIS = "Horizontal";

	private const float MAX_SPEED = 25;
	private const float FWD_ACC_RATIO = 0.75f;
	private const float MIN_FWD_DECC = 1.5f;
	private const float FWD_DAMPING_RATIO = 0.075f;
	private const float MIN_FWD_DAMPING = 0.75f;

	private const float MAX_ROT_ANG_SPEED = 2.5f;
	private const float ROT_ANG_ACC_RATIO = 4.5f;
	private const float ROT_ANG_DECC_RATIO = 9f;
	private const float ROT_SCALE_DOWN_SPEED = 0.2f * MAX_SPEED;

	private const float MAX_STEER_ANG_SPEED = 1.75f;
	private const float STEER_ANG_ACC_RATIO = 3.5f;
	private const float STEER_LANDED_ANG_ACC_RATIO = 14f;
	private const float STEER_ANG_DECC_RATIO = 9f;
	private const float STEER_SCALE_DOWN_SPEED = 0.2f * MAX_SPEED;

	private static float STEER_FORCE_DIFF_MULT;
	private static float ORTHO_FORCE_DIFF_MULT;

	private const float GRAVITY_RADIUS = 75.0f;
	private static float DRAG_COEFF;
	private const float GRAVITY_ACC = 7.5f;

	private const string SPEED_UI_KEY = "Speed";

	// Related to gravity
	private const float SCAN_INTERVAL = 0.2f;
	private static int SCAN_FRAME_SKIP;
	private int scan_cur_frame = 0;

	private float thrustInput;
	private float brakeInput;
	private float fwdInput;
	private float orthoInput;

	private Rigidbody body;

	private PController fwdMotor;
	private PController fwdDamping;

	private static float BOUNDS_SIZE;
	private const float BOUNDS_PUSH_RANGE = 10f;
	private const float BOUNDS_PUSH_MAG = 5f;

	private const float PLATFORM_GRAVITY_MAX_HEIGHT = 25f;
	private const float PLATFORM_GRAVITY_MIN_HEIGHT = -10f;
	// Max height from the platform to the ship when the ship is still considered as landed
	private static float PLATFORM_MAX_LANDING_HEIGHT;
	private BoxCollider platformCollider;
	private bool landedOnPlatform = false;

	void Awake() {
		body = GetComponent<Rigidbody> ();
		body.inertiaTensorRotation = Quaternion.identity;
		DRAG_COEFF = body.mass * 0.5f;
		STEER_FORCE_DIFF_MULT = 20f * body.mass;
		ORTHO_FORCE_DIFF_MULT = 10f * body.mass;
		SCAN_FRAME_SKIP = (int)Mathf.Round(SCAN_INTERVAL / Time.fixedDeltaTime);

		fwdMotor = new PController (FWD_ACC_RATIO);
		fwdDamping = new PController (FWD_DAMPING_RATIO);
		fwdDamping.SetMinOutput(MIN_FWD_DAMPING);

		BOUNDS_SIZE = BoundsFront.localScale.x / 2f;

		platformCollider = PlatformPrefab.GetComponent<BoxCollider> ();

		float platformSize = platformCollider.size.y * PlatformPrefab.transform.localScale.y / 2f;
		CapsuleCollider shipCollider = GetComponent<CapsuleCollider> ();
		float shipSize = shipCollider.height * shipCollider.transform.localScale.y / 2f;
		float maxLandedOffset = 0.5f;
		PLATFORM_MAX_LANDING_HEIGHT = platformSize + shipSize + maxLandedOffset;
	}

	void FixedUpdate()
	{
		thrustInput = Input.GetAxis (THRUST_AXIS);
		brakeInput = Input.GetAxis (BRAKE_AXIS);
		fwdInput = Input.GetAxis (FWD_AXIS);
		orthoInput = Input.GetAxis (ORTHO_AXIS);

		ApplyFwdForce ();
		ApplyOrthoForce ();
		ApplySteeringForce ();

		scan_cur_frame++;
		if (scan_cur_frame >= SCAN_FRAME_SKIP) {
			UpdateGravityObjects();
			scan_cur_frame = 0;
		}
		gravCnt = sphereGravityObjects.Count;
		ApplyGravity ();
		DebugUI.UpdateVar ("Grav", gravCnt.ToString());
		ApplyBoundsForce ();

		ApplyRotation ();
		ApplySteering ();
		ApplyYRotation ();
	}

	void ApplyFwdForce()
	{
		float targetSpeed = GetTargetSpeed ();
		bool engineOn = IsEngineOn ();
		Vector3 fwd = body.transform.forward;
		float fwdSpeed = Vector3.Dot (fwd, body.velocity);
		DebugUI.UpdateVar (SPEED_UI_KEY, fwdSpeed.ToString ("0.0"));

		PController controller;
		if (engineOn || landedOnPlatform) {
			controller = fwdMotor;

			// Braking or (landed and no controls)
			if (targetSpeed < 0.1f) {
				fwdMotor.SetMinOutput (MIN_FWD_DECC);
			} else { // Throttling
				fwdMotor.ClearMinOutput ();
			}
		} else {
			controller = fwdDamping;
		}

		float fwdSpeedDiff = targetSpeed - fwdSpeed;
		controller.SetMaxOutput (fwdSpeedDiff / Time.fixedDeltaTime);
		float fwdAcc = controller.GetOutput(fwdSpeed, targetSpeed);
		// F = m * a
		Vector3 fwdForce = body.mass * fwdAcc * fwd;
		body.AddForce (fwdForce);
	}

	private float GetTargetSpeed() {
		if (brakeInput > 0.1f) {
			return 0f;
		} else if (thrustInput > 0.1f) {
			return MAX_SPEED;
		}
		// Engine turned off
		Debug.Assert(IsEngineOn() == false);
		return 0f;
	}

	private bool IsEngineOn() {
		return (thrustInput > 0.1f) || (brakeInput > 0.1f);
	}

	// Damping only
	void ApplyOrthoForce() {
		Vector3 right = body.transform.right;
		float orthoSpeed = Vector3.Dot (right, body.velocity);

		// F = m * dv/dt
		float maxForceMag = body.mass * Mathf.Abs(orthoSpeed)/Time.fixedDeltaTime;

		float yAngSpeed = Vector3.Dot (body.transform.forward, body.angularVelocity);

		float dragForceMag = Mathf.Abs (orthoSpeed) * DRAG_COEFF;
		float diffForceMag = Mathf.Abs(ORTHO_FORCE_DIFF_MULT * yAngSpeed);

		float orthoForceMag = dragForceMag + diffForceMag;

		orthoForceMag = Mathf.Min (orthoForceMag, maxForceMag);

		Vector3 dragForce = -Mathf.Sign(orthoSpeed) * orthoForceMag * right ;
		body.AddForce (dragForce);
	}

	void ApplyRotation()
	{
		float targetAngSpeed;
		float angAccRatio;
		float shipFwdSpeedAbs = Mathf.Abs(Vector3.Dot(body.transform.forward, body.velocity));
		if (Mathf.Abs (orthoInput) > 0.1f) {
			targetAngSpeed = -Mathf.Sign (orthoInput) * MAX_ROT_ANG_SPEED;
			// Make rotation slower at a slow linear velocity
			float scaleDownRatio = Mathf.Min (shipFwdSpeedAbs / ROT_SCALE_DOWN_SPEED, 1f);
			targetAngSpeed *= scaleDownRatio;
			angAccRatio = ROT_ANG_ACC_RATIO;
		} else {
			targetAngSpeed = 0f;
			angAccRatio = ROT_ANG_DECC_RATIO;
		}

		Vector3 fwd = body.transform.forward;
		float fwdAngSpeed = Vector3.Dot (fwd, body.angularVelocity);

		float fwdAngSpeedDiff = targetAngSpeed - fwdAngSpeed;
		float angAcc = fwdAngSpeedDiff * angAccRatio;
		float torqueMag = body.inertiaTensor.z * angAcc;
		Vector3 relTorque = new Vector3(0, 0, torqueMag);
		body.AddRelativeTorque (relTorque);
	}

	void ApplySteering()
	{
		float targetAngSpeed;
		float angAccRatio;

		float shipFwdSpeedAbs = Mathf.Abs(Vector3.Dot(body.transform.forward, body.velocity));
		if (Mathf.Abs (fwdInput) > 0.1f) {
			targetAngSpeed = Mathf.Sign (fwdInput) * MAX_STEER_ANG_SPEED;
			// Make steering slower at a slow linear velocity
			float scaleDownRatio = Mathf.Min (shipFwdSpeedAbs / STEER_SCALE_DOWN_SPEED, 1f);
			targetAngSpeed *= scaleDownRatio;

			// Increase steering strength when landed: otherwise the ship
			// is unable to take off from a platform
			angAccRatio = landedOnPlatform ? STEER_LANDED_ANG_ACC_RATIO : STEER_ANG_ACC_RATIO;
		} else {
			targetAngSpeed = 0f;
			angAccRatio = STEER_ANG_DECC_RATIO;
		}

		Vector3 right = body.transform.right;
		float xAngSpeed = Vector3.Dot (right, body.angularVelocity);

		float xAngSpeedDiff = targetAngSpeed - xAngSpeed;
		float angAcc = xAngSpeedDiff * angAccRatio;
		float torqueMag = body.inertiaTensor.x * angAcc;
		Vector3 relTorque = new Vector3(torqueMag, 0, 0);
		body.AddRelativeTorque (relTorque);
	}

	// Damping only
	void ApplyYRotation() {
		Vector3 up = body.transform.up;
		float upAngSpeed = Vector3.Dot (up, body.angularVelocity);

		float angAcc = -upAngSpeed / Time.fixedDeltaTime;
		float torqueMag = body.inertiaTensor.y * angAcc;
		Vector3 relTorque = new Vector3(0, torqueMag, 0);
		body.AddRelativeTorque (relTorque);
	}

	void ApplySteeringForce ()
	{
		// Keep Y velocity zero
		Vector3 up = body.transform.up;
		float upSpeed = Vector3.Dot (up, body.velocity);

		DebugUI.UpdateVar ("UpSpeed", upSpeed.ToString ("0.0"));

		// F = m * dv/dt
		float maxForceMag = body.mass * Mathf.Abs(upSpeed)/Time.fixedDeltaTime;

		Vector3 right = body.transform.right;
		float xAngSpeed = Vector3.Dot (right, body.angularVelocity);

		float dragForceMag = Mathf.Abs (upSpeed) * DRAG_COEFF;
		float diffForceMag = Mathf.Abs(STEER_FORCE_DIFF_MULT * xAngSpeed);

		float steeringForceMag = dragForceMag + diffForceMag;

		steeringForceMag = Mathf.Min (steeringForceMag, maxForceMag);
		Vector3 dragForce = -Mathf.Sign(upSpeed) * steeringForceMag * up;
		body.AddForce (dragForce);
	}

	// Gravity towards the center of the sphere
	private List<GameObject> sphereGravityObjects = new List<GameObject> ();
	// Gravity in a single direction
	private List<GameObject> directionalGravityObjects = new List<GameObject> ();
	// Debug variable for number of gravity objects affecting the ship
	private int gravCnt = 0;

	void UpdateGravityObjects() {
		// TODO: delay by one frame per player number
		Collider[] hitColliders = Physics.OverlapSphere(body.transform.position, GRAVITY_RADIUS);

		sphereGravityObjects.Clear ();
		// TODO: filter duplicates
		foreach (Collider collider in hitColliders) {
			GameObject obj = collider.gameObject;
			if (obj.tag.Contains ("SPHERE_GRAVITY")) {
				sphereGravityObjects.Add (obj);
			}
		}

		directionalGravityObjects.Clear ();
		foreach (Collider collider in hitColliders) {
			GameObject obj = collider.gameObject;
			if (obj.tag.Contains ("DIR_GRAVITY")) {
				directionalGravityObjects.Add (obj);
			}
		}
	}

	void ApplyGravity() {

		landedOnPlatform = false;

		// TODO: filter duplicates eg. GameObject with two colliders
		foreach(GameObject obj in sphereGravityObjects) {
			Vector3 toObj = obj.transform.position - body.transform.position;
			toObj.Normalize ();

			float forceMag = body.mass * GRAVITY_ACC;
			body.AddForce (forceMag * toObj);
		}

		// Warning: The platform is expected to have Y up rotation
		foreach (GameObject obj in directionalGravityObjects) {

			Vector3 platformGravityArea = new Vector3 (platformCollider.size.x, PLATFORM_GRAVITY_MAX_HEIGHT*2f, platformCollider.size.z);
			float rotation = obj.transform.rotation.eulerAngles.y;
			Vector3 rotatedArea = Quaternion.AngleAxis (rotation, Vector3.up) * platformGravityArea;
			platformGravityArea = new Vector3 (Mathf.Abs (rotatedArea.x), rotatedArea.y, Mathf.Abs (rotatedArea.z));

			Bounds platformBounds = new Bounds(obj.transform.position, platformGravityArea);

			if (!platformBounds.Contains (transform.position)) {
				return;
			}

			float height = transform.position.y - obj.transform.position.y;
			if (height < PLATFORM_GRAVITY_MIN_HEIGHT) {
				return;
			}

			float platformToShip = body.position.y - obj.transform.position.y;

			if (platformToShip > 0f && platformToShip < PLATFORM_MAX_LANDING_HEIGHT) {
				float angleBetween = Vector3.Angle (obj.transform.up, transform.up);
				if (Mathf.Abs (angleBetween) < 45f || Mathf.Abs (angleBetween) > 135f) {
					landedOnPlatform = true;
				}
			}

			gravCnt++;

			Vector3 gravityDir = Vector3.down;

			float forceMag = body.mass * GRAVITY_ACC;
			body.AddForce (forceMag * gravityDir);
		}
	}

	// Toss away from the game bounds if very close
	// This will prevent getting stuck eg. in a corner
	void ApplyBoundsForce() {
		ApplyBoundsForceAxis (0, body.transform.position.x);
		ApplyBoundsForceAxis (1, body.transform.position.y);
		ApplyBoundsForceAxis (2, body.transform.position.z);
	}

	private void ApplyBoundsForceAxis(int axisIndex, float position) {
		if (BOUNDS_SIZE - Mathf.Abs (position) < BOUNDS_PUSH_RANGE) {
			float sign = -Mathf.Sign (position);
			Vector3 dir;
			if (axisIndex == 0) {
				dir = new Vector3 (sign, 0, 0);
			}
			else if (axisIndex == 1) {
				dir = new Vector3 (0, sign, 0);
			}
			else if (axisIndex == 2) {
				dir = new Vector3 (0, 0, sign);
			} else {
				throw new Exception ("Unhandled state");
			}
			// F = m*a
			body.AddForce (dir*BOUNDS_PUSH_MAG*body.mass);
		}
	}
}
