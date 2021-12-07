using NonStandard.Data;
using NonStandard.Process;
using System.Collections.Generic;
using UnityEngine;

public class MinimizableWindow : MonoBehaviour
{
	public List<RectTransform> concealable;
	public enum State { Normal, Minimized, Maximized }
	public State state = State.Normal;
	public Vector2 position, size;
	public UnityEvent_Vector2 onMinimize;
	public UnityEvent_Vector2 onMaximize;
	public UnityEvent_Vector2 onRestore;
	public void Start() {
		RectTransform rect = GetComponent<RectTransform>();
		size = rect.sizeDelta;
		position = rect.position;
	}
	public void Minimize() {
		if (concealable.Count == 0) return;
		RectTransform rect = GetComponent<RectTransform>();
		size = rect.sizeDelta;
		position = rect.position;
		Rect hideRect = concealable[0].rect;
		for(int i = 0; i < concealable.Count; ++i) {
			Rect r = concealable[i].rect;
			if (r.xMin < hideRect.xMin) hideRect.xMin = r.xMin;
			if (r.yMin < hideRect.yMin) hideRect.yMin = r.yMin;
			if (r.xMax > hideRect.xMax) hideRect.xMax = r.xMax;
			if (r.yMax > hideRect.yMax) hideRect.yMax = r.yMax;
			concealable[i].gameObject.SetActive(false);
		}
		hideRect.x = 0;
		//rect.sizeDelta = size - hideRect.size;
		//Show.Log(size + " " + hideRect);
		rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y - hideRect.height);
		state = State.Minimized;
		onMinimize?.Invoke(rect.sizeDelta);
	}
	public void Restore() {
		RectTransform rect = GetComponent<RectTransform>();
		for (int i = 0; i < concealable.Count; ++i) {
			concealable[i].gameObject.SetActive(true);
		}
		rect.sizeDelta = size;
		if (state == State.Maximized) {
			rect.position = position;
		}
		state = State.Normal;
		onRestore?.Invoke(rect.sizeDelta);
	}
	public void Maximize() {
		RectTransform rect = GetComponent<RectTransform>();
		size = rect.sizeDelta;
		position = rect.position;
		RectTransform parentRect = rect.parent != null ? rect.parent.GetComponent<RectTransform>() : null;
		rect.sizeDelta = parentRect.sizeDelta;
		//rect.position = rect.pivot * parentRect.sizeDelta;
		rect.anchoredPosition = (rect.pivot-rect.anchorMin) * parentRect.sizeDelta;
		//Show.Log(rect.anchoredPosition + " = " + parentRect.sizeDelta + " * (" + rect.pivot+ " - "+rect.anchorMin+")");
		state = State.Maximized;
		onMaximize?.Invoke(rect.sizeDelta);
	}
	public void ToggleMinimizeRestore() {
		State s = state;
		if (s != State.Normal) { Restore(); }
		// giving at least one frame to the normal view gives any nested scroll areas a chance to update themselves correctly
		if (s != State.Minimized) { Proc.Enqueue(Minimize); }
	}
	public void ToggleMaximizeRestore() {
		State s = state;
		if (s != State.Normal) { Restore(); }
		// giving at least one frame to the normal view gives any nested scroll areas a chance to update themselves correctly
		if (s != State.Maximized) { Proc.Enqueue(Maximize); }
	}
}
