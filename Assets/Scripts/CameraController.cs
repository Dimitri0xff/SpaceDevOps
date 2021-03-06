﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour {

	public static CameraController Instance;

	public GameObject Target {
		get { return target; }
		set {
			target = value;
			targetRenderer = target.GetComponent<MeshRenderer> ();
			cameraUp = target.transform.up;
			GetComponent<BoundsRenderer> ().ShipTransform = target.transform;
			targetBody = target.GetComponent<Rigidbody> ();
			cameraUp = target.transform.up;
			playerController = target.GetComponent<PlayerController> ();
		}
	}

	private GameObject target;

	private const string CAMERA_BUTTON = "Camera";
	// Camera will be flipped if view hits a large object
	private const string LARGE_OBJECT_TAG = "LARGE";
	private static Vector3 POS_OFFSET_DIR = new Vector3 (0, 0.05f, -0.88f);
	private const float POS_OFFSET_MAG_DEFAULT = 11.41f;
	private const float POS_SMOOTHING_DURATION = 0.4f;
	private static float UP_SMOOTHING_STEP;
	private const float LOOK_AHEAD_DISTANCE = 4f;
	private const float Y_ROT_ANG_VEL_RATIO = 21f;
	private float LANDED_Y_ROT_ANGLE = 25f;

	private MeshRenderer targetRenderer;
	private Rigidbody targetBody;
	private Vector3 cameraUp;
	// Shift Y axis to negative value if current view collides with a large object
	private int offsetYAxisSign = 1;
	private Vector3 velocity = Vector3.zero;
	// false means it's Third person camera
	private bool firstPersonCamera = false;

	private PlayerController playerController;

	void Awake() {
		if (Instance == null) {
			Instance = this;
		} else if(Instance != this) {
			Destroy (this);
		}
	}

	void Start() {
		UP_SMOOTHING_STEP = 4f * Time.fixedDeltaTime;
	}

	public void Reset() {
		Transform targetTr = target.transform;

		cameraUp = targetTr.up;
		if (firstPersonCamera) {
			transform.position = targetTr.position;
			transform.rotation = targetTr.rotation;
		} else {
			Vector3 desiredPos = targetTr.position + targetTr.rotation * (POS_OFFSET_DIR * POS_OFFSET_MAG_DEFAULT);
			transform.position = desiredPos;
			transform.LookAt (targetTr.position, cameraUp);
		}
	}

	void FixedUpdate () {
		if (target == null) {
			return;
		}
		Transform targetTr = target.transform;
		if (Input.GetButtonDown (CAMERA_BUTTON)) {
			firstPersonCamera = !firstPersonCamera;
		}

		if (firstPersonCamera) {
			// TODO: will need a better solution for Networked Multiplayer, see:
			// https://answers.unity.com/questions/63261/network-restrictive-rendering.html
			targetRenderer.enabled = false;
			transform.position = targetTr.position;
			transform.rotation = targetTr.rotation;
			cameraUp = targetTr.up; // Needed to transition smoothly into Third Person Camera
		} else {
			UpdateThirdPersonCamera (targetTr);
		}
	}

	private void UpdateThirdPersonCamera(Transform targetTr) {
		targetRenderer.enabled = true;

		// Direction from the target to the camera
		Vector3 offsetDir = new Vector3 (POS_OFFSET_DIR.x, POS_OFFSET_DIR.y, POS_OFFSET_DIR.z);

		float xAngSpeed = Vector3.Dot (targetTr.right, targetBody.angularVelocity);
		// Additional rotation on the x axis as needed
		float xRotAngle = Y_ROT_ANG_VEL_RATIO*xAngSpeed;
		if (playerController.IsLanded) {
			xRotAngle += LANDED_Y_ROT_ANGLE;
		}
		// Apply the rotation on the x axis
		offsetDir = Quaternion.AngleAxis (xRotAngle, Vector3.right) * offsetDir;

		// Get camera distance -- avoid blocked camera by reducing distance or flipping rotation on the x axis
		float cameraDistSigned = calculateCameraDist (targetTr, offsetDir);
		float cameraDist = Mathf.Abs (cameraDistSigned);

		offsetDir.y *= Mathf.Sign(cameraDistSigned);
		Vector3 desiredPos = targetTr.position + targetTr.rotation * (offsetDir * cameraDist);

		Vector3 newPos = Vector3.SmoothDamp (transform.position, desiredPos, ref velocity, POS_SMOOTHING_DURATION,
			Mathf.Infinity, Time.fixedDeltaTime);

		// Must keep the camera Up vector moving otherwise the camera can 'flip' around
		cameraUp = Vector3.Lerp (cameraUp, targetTr.up, UP_SMOOTHING_STEP);

		transform.position = newPos;
		Vector3 lookAhead = targetTr.rotation * new Vector3 (0, 0, LOOK_AHEAD_DISTANCE);
		transform.LookAt (targetTr.position + lookAhead, cameraUp);
	}

	private float calculateCameraDist(Transform targetTr, Vector3 posOffsetDir) {
		RaycastHit hit;
		float posOffsetMag;

		posOffsetMag = POS_OFFSET_MAG_DEFAULT;
		offsetYAxisSign = 1;

		// Bring camera closer if there is a raycast hit
		if (Physics.Raycast(targetTr.position, targetTr.rotation * posOffsetDir, out hit, POS_OFFSET_MAG_DEFAULT)) {

			string tag = hit.transform.gameObject.tag;

			posOffsetMag = hit.distance;

			// The current view is blocked -> check if the Y axis of the camera offset can be shifted to negative to provide better sight
			// 1) The object hit must have a specific tag
			if (tag.Contains (LARGE_OBJECT_TAG)) {

				Vector3 proposedNewDir = new Vector3 (posOffsetDir.x, -posOffsetDir.y, posOffsetDir.z);
				Vector3 proposedDirAbs = targetTr.rotation * proposedNewDir;
				// 2) The new view has to be free
				bool raycast2Result = Physics.Raycast (targetTr.position, proposedDirAbs, out hit, POS_OFFSET_MAG_DEFAULT);
				if (!raycast2Result || !hit.transform.gameObject.tag.Contains (LARGE_OBJECT_TAG)) {

					offsetYAxisSign = -1;
					// Check max camera distance at the new view
					if (raycast2Result) {
						posOffsetMag = hit.distance;
					} else {
						posOffsetMag = POS_OFFSET_MAG_DEFAULT;
					}
				}
			}

		}
		return posOffsetMag * offsetYAxisSign;
	}
}
