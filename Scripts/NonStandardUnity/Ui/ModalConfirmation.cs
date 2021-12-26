using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ModalConfirmation : MonoBehaviour {
	public DescriptionTextWithIcon descriptionArea;
	public RectTransform inputArea;
	public RectTransform modalWindow;
	public GameObject prefab_option;
	public List<Entry> options = new List<Entry>();

	public string Text { get => descriptionArea.Text; set => descriptionArea.Text = value; }
	public Sprite Sprite { get => descriptionArea.Sprite; set => descriptionArea.Sprite = value; }
	public Color32 TextColor { get => descriptionArea.TextColor; set => descriptionArea.TextColor = value; }
	public Color32 SpriteColor { get => descriptionArea.SpriteColor; set => descriptionArea.SpriteColor = value; }

	public bool Active => gameObject.activeSelf;

	public void OkCancel(string text, Action onConfirm) {
		descriptionArea.Text = text;
		descriptionArea.TextColor = Color.black;
		descriptionArea.UseImage = false;
		options = new List<Entry> {
			new Entry("OK", () => { onConfirm.Invoke(); Hide(); }),
			new Entry("Cancel", Hide),
		};
		if (Active) { Refresh(); } else { Show(); }
		if (modalWindow != null) {
			modalWindow.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
		}
	}

	[System.Serializable]
	public class Entry {
		public string text;
		public Sprite image;
		public Color32 textColor = Color.white, imageColor = Color.white;
		public UnityEvent selectionAction;
		/// <summary>
		/// if true, this option will execute code, but not set the current choice index
		/// </summary>
		public bool eventOnly;
		public Entry(string text, object target, string methodName, bool valuePersists = true) {
			if (!string.IsNullOrEmpty(methodName)) {
				selectionAction = new UnityEvent();
				NonStandard.EventBind.On(selectionAction, target, methodName);
			}
			this.text = text;
			eventOnly = !valuePersists;
		}
		public Entry(string text, Action action) {
			this.text = text;
			if (action != null) {
				selectionAction = new UnityEvent();
				selectionAction.AddListener(action.Invoke);
			}
		}
		public void Apply(DescriptionTextWithIcon entryObject) {
			entryObject.Text = text;
			entryObject.TextColor = textColor;
			entryObject.UseImage = image != null;
			if (image != null) {
				entryObject.Sprite = image;
				entryObject.SpriteColor = imageColor;
			}
		}
	}
	public void DoAction(int index) { options[index].selectionAction.Invoke(); }

	private void OnEnable() {
		Refresh();
	}
	public void Refresh() {
		GameObject option = null;
		for (int i = 0; i < options.Count; ++i) {
			if (i >= inputArea.childCount) {
				option = Instantiate(prefab_option);
				option.transform.SetParent(inputArea);
			} else {
				option = inputArea.GetChild(i).gameObject;
			}
			option.SetActive(true);
			option.name = options[i].text;
			DescriptionTextWithIcon itm = option.GetComponent<DescriptionTextWithIcon>();
			options[i].Apply(itm);
			Button btn = option.GetComponentInChildren<Button>();
			btn.onClick.RemoveAllListeners();
			btn.onClick.AddListener(options[i].selectionAction.Invoke);
		}
		for (int i = options.Count; i < inputArea.childCount; ++i) {
			option = inputArea.GetChild(i).gameObject;
			option.SetActive(false);
		}
	}
	public void Hide() {
		gameObject.SetActive(false);
	}
	public void Show() {
		gameObject.SetActive(true);
	}
}
