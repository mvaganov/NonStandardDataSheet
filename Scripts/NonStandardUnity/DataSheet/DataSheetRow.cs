using NonStandard.Data;
using NonStandard.Extension;
using UnityEngine;

namespace NonStandard.GameUi.DataSheet {
	public class DataSheetRow : MonoBehaviour {
		public RowData rowData;
		private Vector3 targetLocalPosition;
		public int Index { get => transform.GetSiblingIndex(); set { transform.SetSiblingIndex(value); } }
		public object obj => rowData.obj;

		public Vector3 Position {
			get => transform.position + targetLocalPosition;
			set => targetLocalPosition = value - transform.position;
		}
		public Vector3 LocalPosition {
			get => targetLocalPosition;
			set => targetLocalPosition = value;
		}
		private void Update() {
			Vector3 delta = targetLocalPosition - transform.localPosition;
			float manhattan = delta.MagnitudeManhattan();
			if (manhattan == 0) return;
			if (manhattan < 1) { transform.localPosition = targetLocalPosition; return; }
			transform.localPosition += (delta / 2);
		}
	}
}