using NonStandard.Ui;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UiHoverPopup : MonoBehaviour {
	GameObject lastErrorInput;
	[HideInInspector] public Color defaultColor;
	[System.Serializable] public class MessageType {
		public string name;
		public Color bgColor;
		public MessageType(string n) : this(n, Color.clear) { }
		public MessageType(string n, Color c) { name = n;bgColor = c; }
	}
	public List<MessageType> messageTypes = new List<MessageType>() {
		new MessageType("pop"), new MessageType("err",new Color(1,.75f,.75f))
	};

	public string Message { get => UiText.GetText(gameObject); }

	public void UncolorLastInput() {
		if (lastErrorInput == null) { return; }
		Image img = lastErrorInput.GetComponent<Image>();
		img.color = defaultColor;
	}
	public void Set(string messageType, GameObject errorInputObject, string text) {
		MessageType mt = messageTypes.Find(m => m.name == messageType);
		UiText.SetText(gameObject, text);
		gameObject.SetActive(true);
		UncolorLastInput();
		if (errorInputObject != null) {
			Image img = errorInputObject.GetComponent<Image>();
			if (mt != null && img.color != mt.bgColor) { defaultColor = img.color; }
			if (mt != null) {
				img.color = mt.bgColor;
			} else {
				img.color = defaultColor;
			}
		}
		lastErrorInput = errorInputObject;
	}
	public void Hide() {
		gameObject.SetActive(false);
		UncolorLastInput();
		lastErrorInput = null;
	}
	public bool IsVisible => gameObject.activeInHierarchy;
}
