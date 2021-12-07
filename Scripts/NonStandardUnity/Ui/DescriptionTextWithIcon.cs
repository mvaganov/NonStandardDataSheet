using NonStandard.Extension;
using NonStandard.Ui;
using UnityEngine;
using UnityEngine.UI;

public class DescriptionTextWithIcon : MonoBehaviour {
	public GameObject textObject;
	public Image image;
	public Direction2DBasic imageOrientation = Direction2DBasic.Left;
	public bool UseImage {
		get => image.gameObject.activeSelf;
		set {
			RectTransform irect = image.rectTransform;
			RectTransform trect = textObject.GetComponent<RectTransform>();
			Rect imgR = irect.rect;
			if (value) {
				if (imageOrientation.HasFlag(Direction2DBasic.Top) && !imageOrientation.HasFlag(Direction2DBasic.Bottom)) { trect.SetTop(imgR.height); }
				if (imageOrientation.HasFlag(Direction2DBasic.Left) && !imageOrientation.HasFlag(Direction2DBasic.Right)) { trect.SetLeft(imgR.width); }
				if (imageOrientation.HasFlag(Direction2DBasic.Right) && !imageOrientation.HasFlag(Direction2DBasic.Left)) { trect.SetRight(imgR.width); }
				if (imageOrientation.HasFlag(Direction2DBasic.Bottom) && !imageOrientation.HasFlag(Direction2DBasic.Top)) { trect.SetBottom(imgR.height); }
			} else {
				if (imageOrientation.HasFlag(Direction2DBasic.Top) && !imageOrientation.HasFlag(Direction2DBasic.Bottom)) { trect.SetTop(irect.GetTop()); }
				if (imageOrientation.HasFlag(Direction2DBasic.Left) && !imageOrientation.HasFlag(Direction2DBasic.Right)) { trect.SetLeft(irect.GetLeft()); }
				if (imageOrientation.HasFlag(Direction2DBasic.Right) && !imageOrientation.HasFlag(Direction2DBasic.Left)) { trect.SetRight(irect.GetRight()); }
				if (imageOrientation.HasFlag(Direction2DBasic.Bottom) && !imageOrientation.HasFlag(Direction2DBasic.Top)) { trect.SetBottom(irect.GetBottom()); }
			}
			image.gameObject.SetActive(value);
		}
	}
	public Sprite Sprite { get => image != null ? image.sprite : null; set { if (image == null) { return; } image.sprite = value; } }
	public string Text { get => UiText.GetText(textObject); set => UiText.SetText(textObject, value); }
	public Color32 TextColor { get => UiText.GetColor(textObject); set => UiText.SetColor(textObject, value); }
	public Color32 SpriteColor { get => image != null ? image.color : Color.clear; set { if (image == null) { return; } image.color = value; } }
	private void OnEnable() { Refresh(); }
	public void Refresh() {
		if (textObject == null) textObject = UiText.GetTextComponent(textObject).gameObject;
		if (image == null) image = GetComponentInChildren<Image>();
		UseImage = UseImage;
	}
}
