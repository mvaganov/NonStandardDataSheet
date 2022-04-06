using NonStandard.Utility.UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NonStandard.GameUi.DataSheet {
	public class ColumnHeaderRefresh : MonoBehaviour {
		UnityDataSheet UDS() { return GetComponentInParent<UnityDataSheet>(); }
		private void Init() {
			UnityDataSheet uds = UDS();
			if (uds == null) { return; }
			Button b = GetComponent<Button>();
			EventBind.IfNotAlready(b.onClick, uds, uds.RefreshData);
		}
		private void Start() {
			Init();
		}
	}
}