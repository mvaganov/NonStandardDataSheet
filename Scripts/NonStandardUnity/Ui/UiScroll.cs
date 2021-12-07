using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NonStandard.Ui {
	public class UiScroll : MonoBehaviour {
		public RectTransform contentRectangle;
		public void MatchLocalX(Vector2 whichDimension) {
			Vector3 lp = transform.localPosition;
			lp.x = contentRectangle.localPosition.x;
			transform.localPosition = lp;
		}
	}
}