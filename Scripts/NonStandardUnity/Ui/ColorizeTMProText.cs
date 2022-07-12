using System.Collections.Generic;
using UnityEngine;

public class ColorizeTMProText : MonoBehaviour {
	public List<SyntaxColor> _colorList;
	private static Color _unsetColor = new Color(1f, 0f, 1f, 0f);
	public Color _defaultColor = _unsetColor;
	[System.Serializable] public class SyntaxColor {
		public string syntax;
		public Color color;
	}

	void FindDefaultTextColor() {
		if (_defaultColor != _unsetColor) return;
		TMPro.TMP_Text txt = GetComponentInChildren<TMPro.TMP_Text>();
		_defaultColor = txt.color;
	}

	void Start() {
		// generate dictionary of syntax tree color list
		// calculate syntax tree
		// go through text and apply color based on color dictionary
	}

	void Update() {

	}
}
