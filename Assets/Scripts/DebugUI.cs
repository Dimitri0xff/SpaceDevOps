﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DebugUI : MonoBehaviour {

	public GameObject m_NamePrefab;
	public GameObject m_ValuePrefab;

	private Dictionary<string, Text> values = new Dictionary<string, Text>();

	public void RegisterVar(string name) {
		GameObject nameObject = (GameObject)Instantiate (m_NamePrefab);
		Text nameText = nameObject.GetComponent<Text> ();
		nameText.text = name;
		nameText.transform.SetParent (transform);

		GameObject valueObject = (GameObject)Instantiate (m_NamePrefab);
		Text valueText = valueObject.GetComponent<Text> ();
		valueText.text = "";
		valueText.transform.SetParent (transform);

		values.Add (name, valueText);
	}

	public void UpdateVar(string name, string value) {
		values [name].text = value;
	}
}
