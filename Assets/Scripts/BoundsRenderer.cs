﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoundsRenderer : MonoBehaviour {

	public Material Material;
	public bool DebugBounds = false;
	public Transform ShipTransform;

	private static float BOUNDS_SIZE = -1;
	private const float BOUNDS_VISIBLE_RANGE = 80f;
	private const int BOUNDS_CORE_CNT = 5;
	private const int BOUNDS_FADE_CNT = 7;

	private new Camera camera;

	void Awake() {
		camera = GetComponent<Camera> ();
	}

	void Start() {
		// FIXME: Get from object size (SceneManager)!
		// For some reason SceneManager.Instance was still null here, even it is initialized in Awake()
		BOUNDS_SIZE = 1200/2;
	}

	void OnPostRender() {
		if (ShipTransform == null) {
			return;
		}

		Vector3 pos = ShipTransform.position;

		GL.PushMatrix();
		GL.LoadProjectionMatrix(camera.projectionMatrix);
		Material.SetPass(0);
		GL.Begin(GL.LINES);

		if (DebugBounds) {
			RenderDebugBounds ();
		}

		CheckRenderBounds (0, pos.x, pos.y, pos.z);
		CheckRenderBounds (1, pos.y, pos.x, pos.z);
		CheckRenderBounds (2, pos.z, pos.x, pos.y);

		GL.End();
		GL.PopMatrix();
	}

	private void RenderDebugBounds() {
		GL.Color(Color.yellow);
		GL.Vertex (new Vector3 (-BOUNDS_SIZE, -BOUNDS_SIZE, -BOUNDS_SIZE));
		GL.Vertex (new Vector3 (BOUNDS_SIZE, -BOUNDS_SIZE, -BOUNDS_SIZE));
		GL.Vertex (new Vector3 (-BOUNDS_SIZE, -BOUNDS_SIZE, -BOUNDS_SIZE));
		GL.Vertex (new Vector3 (-BOUNDS_SIZE, BOUNDS_SIZE, -BOUNDS_SIZE));
		GL.Vertex (new Vector3 (-BOUNDS_SIZE, -BOUNDS_SIZE, -BOUNDS_SIZE));
		GL.Vertex (new Vector3 (-BOUNDS_SIZE, -BOUNDS_SIZE, BOUNDS_SIZE));
		GL.Vertex (new Vector3 (BOUNDS_SIZE, BOUNDS_SIZE, BOUNDS_SIZE));
		GL.Vertex (new Vector3 (-BOUNDS_SIZE, BOUNDS_SIZE, BOUNDS_SIZE));
		GL.Vertex (new Vector3 (BOUNDS_SIZE, BOUNDS_SIZE, BOUNDS_SIZE));
		GL.Vertex (new Vector3 (BOUNDS_SIZE, -BOUNDS_SIZE, BOUNDS_SIZE));
		GL.Vertex (new Vector3 (BOUNDS_SIZE, BOUNDS_SIZE, BOUNDS_SIZE));
		GL.Vertex (new Vector3 (BOUNDS_SIZE, BOUNDS_SIZE, -BOUNDS_SIZE));
		GL.Vertex (new Vector3 (BOUNDS_SIZE, BOUNDS_SIZE, -BOUNDS_SIZE));
		GL.Vertex (new Vector3 (-BOUNDS_SIZE, BOUNDS_SIZE, -BOUNDS_SIZE));
		GL.Vertex (new Vector3 (BOUNDS_SIZE, BOUNDS_SIZE, -BOUNDS_SIZE));
		GL.Vertex (new Vector3 (BOUNDS_SIZE, -BOUNDS_SIZE, -BOUNDS_SIZE));
		GL.Vertex (new Vector3 (BOUNDS_SIZE, -BOUNDS_SIZE, BOUNDS_SIZE));
		GL.Vertex (new Vector3 (-BOUNDS_SIZE, -BOUNDS_SIZE, BOUNDS_SIZE));
		GL.Vertex (new Vector3 (BOUNDS_SIZE, -BOUNDS_SIZE, BOUNDS_SIZE));
		GL.Vertex (new Vector3 (BOUNDS_SIZE, -BOUNDS_SIZE, -BOUNDS_SIZE));
		GL.Vertex (new Vector3 (-BOUNDS_SIZE, BOUNDS_SIZE, BOUNDS_SIZE));
		GL.Vertex (new Vector3 (-BOUNDS_SIZE, -BOUNDS_SIZE, BOUNDS_SIZE));
		GL.Vertex (new Vector3 (-BOUNDS_SIZE, BOUNDS_SIZE, BOUNDS_SIZE));
		GL.Vertex (new Vector3 (-BOUNDS_SIZE, BOUNDS_SIZE, -BOUNDS_SIZE));
	}

	private void CheckRenderBounds(int zIndex, float vx, float vy, float vz) {
		if (BOUNDS_SIZE - Mathf.Abs (vx) > BOUNDS_VISIBLE_RANGE) {
			return;
		}
		int dir = vx >= 0 ? 1 : -1;
		RenderBounds (zIndex, vy, vz, dir, BOUNDS_SIZE);
	}

	private void RenderBounds(int zIndex, float xCenter, float yCenter, int dir, float bounds) {
		for (int i = 1; i <= BOUNDS_CORE_CNT; i++) {
			GL.Color(Color.yellow);
			RenderSquare (zIndex, xCenter, yCenter, dir * bounds, i, bounds);
		}

		for (int i = 1; i <= BOUNDS_FADE_CNT; i++) {
			GL.Color(new Color(1f, 1f, 0, 1f/BOUNDS_FADE_CNT*(BOUNDS_FADE_CNT-i)));
			RenderSquare (zIndex, xCenter, yCenter, dir * bounds, BOUNDS_CORE_CNT + i, bounds);
		}
	}

	private void RenderSquare(int zIndex, float xCenter, float yCenter, float zCenter, float size, float bounds) {
		float xL = Mathf.Max (-bounds, xCenter - size);
		float yL = Mathf.Max (-bounds, yCenter - size);
		float xH = Mathf.Min (bounds, xCenter + size);
		float yH = Mathf.Min (bounds, yCenter + size);
		Vector3 upperR;
		Vector3 bottomR;
		Vector3 upperL;
		Vector3 bottomL;
		if (zIndex == 2) {
			upperR = new Vector3 (xH, yH, zCenter);
			bottomR = new Vector3 (xH, yL, zCenter);
			upperL = new Vector3 (xL, yH, zCenter);
			bottomL = new Vector3 (xL, yL, zCenter);
		} else if (zIndex == 1) {
			upperR = new Vector3 (xH, zCenter, yH);
			bottomR = new Vector3 (xH, zCenter, yL);
			upperL = new Vector3 (xL, zCenter, yH);
			bottomL = new Vector3 (xL, zCenter, yL);
		} else if (zIndex == 0) {
			upperR = new Vector3 (zCenter, xH, yH);
			bottomR = new Vector3 (zCenter, xH, yL);
			upperL = new Vector3 (zCenter, xL, yH);
			bottomL = new Vector3 (zCenter, xL, yL);
		} else {
			throw new Exception ("Unhandled state");
		}
		GL.Vertex (upperR);
		GL.Vertex (bottomR);
		GL.Vertex (bottomR);
		GL.Vertex (bottomL);
		GL.Vertex (bottomL);
		GL.Vertex (upperL);
		GL.Vertex (upperL);
		GL.Vertex (upperR);
	}
}
