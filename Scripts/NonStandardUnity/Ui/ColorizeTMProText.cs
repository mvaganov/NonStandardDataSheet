using NonStandard.Data.Parse;
using NonStandard.Extension;
using NonStandard.Ui;
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
		Tokenizer tok = new Tokenizer();
		string text = UiText.GetText(gameObject);
		tok.Tokenize(text);
		//for (int i = 0; i < tok.Tokens.Count; i++) {
		//	Debug.
		//}
		Debug.Log(tok.Tokens.Count);
		Debug.Log(tok.Tokens.JoinToString(",", t => {
			return t.ToString();
		}));
		//Debug.Log(tok.DebugPrint());
		// generate dictionary of syntax tree color list
		// calculate syntax tree
		// go through text and apply color based on color dictionary
	}

	void Update() {

	}
}
