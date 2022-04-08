// code by michael vaganov, released to the public domain via the unlicense (https://unlicense.org/)
using UnityEngine;
using UnityEngine.UI;

namespace NonStandard.GameUi.DataSheet {
	public class DataSheetRefreshButton : MonoBehaviour {
		UnityDataSheet UDS() { return GetComponentInParent<UnityDataSheet>(); }
		private void Init() {
			UnityDataSheet uds = UDS();
			if (uds == null) { return; }
			Button b = GetComponent<Button>();
			EventBind.IfNotAlready(b.onClick, uds, uds.QueueRefresh);
		}
		private void Start() {
			Init();
		}
	}
}