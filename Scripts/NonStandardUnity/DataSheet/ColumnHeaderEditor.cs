using UnityEngine;
using TMPro;
using NonStandard.Ui;
using NonStandard.Data.Parse;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using System.Reflection;
using NonStandard.Data;
using NonStandard.Utility.UnityEditor;
using NonStandard.Extension;
using NonStandard.Process;

namespace NonStandard.GameUi.DataSheet {
	public class ColumnHeaderEditor : MonoBehaviour {
		public ModalConfirmation confirmRemoveUi;
		public GameObject columnHeaderObject;
		private ColumnHeader cHeader;
		public UnityDataSheet uds;
		public int column;
		public Type expectedValueType;
		[System.Serializable] public struct ValidColumnEntry {
			public string name;
			public GameObject uiField;
		}
		public List<ValidColumnEntry> columnTypes = new List<ValidColumnEntry>();

		public Color errorColor = new Color(1,.75f,.75f);
		// compiles Token. errors make the field pink, and display the error popup. if valid, refresh valueType dropdown
		public TMP_InputField scriptValue;
		public UiHoverPopup popup;
		public TMP_InputField columnLabel;
		// TODO pick option from validColumnTypes
		public TMP_Dropdown fieldType;
		// TODO another scripted value. should also use error popup
		public TMP_InputField defaultValue;
		// TODO generate based on scriptValue. if type is ambiguous, offer [string, number, integer, Token]
		public TMP_Dropdown valueType;
		// change cHeader.columnSetting.data.width, refresh rows
		public TMP_InputField columnWidth;
		// ignore erroneous values. move column and refresh on change.
		public TMP_InputField columnIndex;
		// TODO confirm dialog. if confirmed, remove from UnityDataSheet and update everything
		public Button trashColumn;

		public void Start() {
			popup.defaultColor = scriptValue.GetComponent<Image>().color;
		}
		public void ClearInputTextBoxListeners() {
			TMP_InputField elementUiInputField = fieldType.GetComponentInChildren<TMP_InputField>();
			if (elementUiInputField != null) {
				//elementUiInputField.onValueChanged.RemoveAllListeners();
				// not enough to remove all listeners apparently.
				elementUiInputField.onValueChanged = new TMP_InputField.OnChangeEvent();
				//Show.Log("unbind from "+elementUiInputField.name);
			}
			scriptValue.onValueChanged.RemoveAllListeners();
			columnLabel.onValueChanged.RemoveAllListeners();
			columnWidth.onValueChanged.RemoveAllListeners();
			columnIndex.onValueChanged.RemoveAllListeners();
			defaultValue.onValueChanged.RemoveAllListeners();
			trashColumn.onClick.RemoveAllListeners();
		}

		public void SetColumnHeader(ColumnHeader columnHeader, UnityDataSheet uds, int column) {
			// unregister listeners before values change, since values are about to change.
			ClearInputTextBoxListeners();

			this.uds = uds;
			this.column = column;
			cHeader = columnHeader;
			TokenErrorLog errLog = new TokenErrorLog();
			// setup script value
			Token t = cHeader.columnSetting.fieldToken;
			//string textA = t.GetAsSmallText();
			//string textB = t.Stringify();
			//string textC = t.StringifySmall();
			//string textD = t.ToString();
			string text = t.GetAsBasicToken();
			//Show.Log("A: "+textA+"\nB:" + textB + "\nC:" + textC + "\nD:" + textD + "\nE:" + text);
			scriptValue.text = text;
			EventBind.On(scriptValue.onValueChanged, this, OnScriptValueEdit);
			// implicitly setup value types dropdown
			OnScriptValueEdit(text);
			// setup column label
			object labelText = cHeader.columnSetting.data.label.Resolve(errLog, uds.data);
			if (errLog.HasError()) { popup.Set("err", defaultValue.gameObject, errLog.GetErrorString()+Proc.Now); return; }
			columnLabel.text = labelText.StringifySmall();
			EventBind.On(columnLabel.onValueChanged, this, OnLabelEdit);
			// setup column width
			columnWidth.text = cHeader.columnSetting.data.widthOfColumn.ToString();
			EventBind.On(columnWidth.onValueChanged, this, OnColumnWidthEdit);
			// setup column index
			columnIndex.text = column.ToString();
			EventBind.On(columnIndex.onValueChanged, this, OnIndexEdit);
			// setup column type
			List<ModalConfirmation.Entry> entries = columnTypes.ConvertAll(c => {
				string dropdownLabel;
				if(c.uiField != null && !string.IsNullOrEmpty(c.name)) {
					dropdownLabel = "/*" + c.name + "*/ " + c.uiField.name;
				} else {
					dropdownLabel = c.name;
				}
				return new ModalConfirmation.Entry(dropdownLabel, null);
			});

			t = cHeader.columnSetting.data.columnUi;
			string fieldTypeText = t.ToString();//cHeader.columnSetting.data.columnUi.GetAsBasicToken();//ResolveString(errLog, null);
			int currentIndex = columnTypes.FindIndex(c=> fieldTypeText.StartsWith(c.uiField.name)) + 1;
			//Show.Log(currentIndex+" field  " + fieldTypeText);
			DropDownEvent.PopulateDropdown(fieldType, entries, this, SetFieldType, currentIndex, true);
			if (currentIndex == 0) {
				DropDownEvent.SetCustomValue(fieldType.gameObject, fieldTypeText);
			}
			TMP_InputField elementUiInputField = fieldType.GetComponentInChildren<TMP_InputField>();
			if (elementUiInputField != null) {
				elementUiInputField.onValueChanged.RemoveAllListeners();
				//Show.Log("bind to "+elementUiInputField.name);
				EventBind.On(elementUiInputField.onValueChanged, this, OnSetFieldTypeText);
			}
			// setup default value
			object defVal = cHeader.columnSetting.defaultValue;
			if (defVal != null) {
				defaultValue.text = defVal.ToString();
			} else {
				defaultValue.text = "";
			}
			EventBind.On(defaultValue.onValueChanged, this, OnSetDefaultValue);
			// setup column destroy option
			EventBind.On(trashColumn.onClick, this, ColumnRemove);
			popup.Hide();
		}
		public void OnSetFieldTypeText(string text) {
			//Show.Log("fieldTypeGettingSet? " + this.fieldType.itemText.text+ " " + text);
			if (!gameObject.activeInHierarchy) { return; }
			if (SetFieldTypeText(text)) {
				uds.RefreshRowAndColumnUi();
			}
		}
		public bool SetFieldTypeText(string text) {
			Tokenizer tokenizer = Tokenizer.Tokenize(text);
			if (tokenizer.HasError()) { popup.Set("err", defaultValue.gameObject, tokenizer.GetErrorString() + Proc.Now); return false; }
			Token t = Token.None;
			List<Token> tokens = Data.Parse.SyntaxTree.FindSubstantiveTerms(tokenizer.Tokens); // ignore comments!
			switch (tokens.Count) {
			case 0: break;
			case 1: t = tokens[0]; break;
			default: t = Tokenizer.GetMasterToken(tokens, text); break;
			}
			//Show.Log("SetFieldTypeText " + text+" -> "+t.GetAsBasicToken()+"\n"+tokenizer.GetMasterToken().Resolve(tokenizer, null).Stringify());
			cHeader.columnSetting.data.columnUi = t;
			popup.Hide();
			return true;
		}
		public void OnSetDefaultValue(string text) {
			object value = null;
			Tokenizer tokenizer = new Tokenizer();
			CodeConvert.TryParseType(expectedValueType, text, ref value, null, tokenizer);
			// parse errors
			if (tokenizer.HasError()) { popup.Set("err", defaultValue.gameObject, tokenizer.GetErrorString()); return; }
			cHeader.columnSetting.defaultValue = value;
			popup.Hide();
		}
		public void SetFieldType(int index) {
			// initial value is for custom text
			if (index == 0) {
				if (!SetFieldTypeText(UiText.GetText(fieldType.gameObject))) { return; }
			} else if (index >= 1 && columnTypes[index-1].uiField != null) {
				cHeader.columnSetting.data.columnUi = new Token(columnTypes[index-1].uiField.name);
			}
			uds.RefreshRowAndColumnUi();
		}
		public void OnLabelEdit(string text) {
			Tokenizer tokenizer = Tokenizer.Tokenize(text);
			if (tokenizer.HasError()) { popup.Set("err", defaultValue.gameObject, tokenizer.GetErrorString()); return; }
			Token token = Token.None;
			switch (tokenizer.Tokens.Count) {
			case 0: break;
			case 1: token = tokenizer.Tokens[0]; break;
			default: token = new Token(text); break;
			}
			cHeader.columnSetting.data.label = token;
			object result = token.Resolve(tokenizer, uds.data);
			if (tokenizer.HasError()) { popup.Set("err", defaultValue.gameObject, tokenizer.GetErrorString()); return; }
			string resultText = result?.ToString() ?? "";
			UiText.SetText(cHeader.gameObject, resultText);
			popup.Hide();
		}
		public void OnColumnWidthEdit(string text) {
			float oldWidth = cHeader.columnSetting.data.widthOfColumn;
			if (float.TryParse(text, out float newWidth)) {
				if (newWidth > 0 && newWidth < 2048) {
					uds.ResizeColumnWidth(column, oldWidth, newWidth);
				} else {
					popup.Set("err", columnWidth.gameObject, "invalid width: " + newWidth + ". Requirement: 0 < value < 2048");
					return;
				}
			}
			popup.Hide();
		}
		public void OnIndexEdit(string text) {
			int oldIndex = cHeader.transform.GetSiblingIndex();
			if (oldIndex != column) {
				popup.Set("err", columnIndex.gameObject, "WOAH PROBLEM! column " + column + " is not the same as childIndex " + oldIndex);
				return;
			}
			int max = uds.GetMaximumUserColumn();
			if (int.TryParse(text, out int newIndex)) {
				if (newIndex > 0 && newIndex < max) {
					uds.MoveColumn(oldIndex, newIndex);
					column = newIndex;
				} else {
					popup.Set("err", columnIndex.gameObject,"invalid index: " + newIndex + ". Requirement: 0 < index < " + max);
					return;
				}
			}
			popup.Hide();
		}
		public void OnScriptValueEdit(string fieldScript) {
			Tokenizer tokenizer = new Tokenizer();
			tokenizer.Tokenize(fieldScript);
			GameObject go = scriptValue.gameObject;
			// parse errors
			if (tokenizer.HasError()) { popup.Set("err", go, tokenizer.GetErrorString()); return; }
			// just one token
			if (tokenizer.Tokens.Count > 1) { popup.Set("err", go, "too many tokens: should only be one value"); return; }
			// try to set the field based on field script
			if(!ProcessFieldScript(tokenizer)) return;
			// refresh column values
			uds.RefreshColumnText(column, tokenizer);
			// failed to set values
			if (tokenizer.HasError()) { popup.Set("err", go, tokenizer.GetErrorString()); return; }
			// success!
			popup.Hide();
		}
		private bool ProcessFieldScript(Tokenizer tokenizer) {
			if (tokenizer.Tokens.Count == 0) {
				expectedValueType = null;
				cHeader.columnSetting.editPath = null;
				return false;
			}
			object value = cHeader.columnSetting.SetFieldToken(tokenizer.Tokens[0], tokenizer);
			// update the expected edit type
			SetExpectedEditType(value);
			// valid variable path
			if (tokenizer.HasError()) { popup.Set("err", scriptValue.gameObject, tokenizer.GetErrorString()); return false; }
			popup.Hide();
			return true;
		}
		public void SetExpectedEditType(object sampleValue) {
			Type sampleValueType = GetEditType();
			if (sampleValueType == null) {
				// set to read only
				expectedValueType = null;
				DropDownEvent.PopulateDropdown(valueType, new string[] { "read only" }, this, null, 0, false);
			} else {
				if (sampleValueType != expectedValueType) {
					// set to specific type
					if (sampleValueType == typeof(object)) {
						sampleValueType = sampleValue.GetType();
						int defaultChoice = -1;
						if (defaultChoice < 0 && CodeConvert.IsIntegral(sampleValueType)) {
							defaultChoice = defaultValueTypes.FindIndex(kvp=>kvp.Key == typeof(long));
						}
						if (defaultChoice < 0 && CodeConvert.IsNumeric(sampleValueType)) {
							defaultChoice = defaultValueTypes.FindIndex(kvp => kvp.Key == typeof(double));
						}
						if (defaultChoice < 0) {// && sampleValueType == typeof(string)) {
							defaultChoice = defaultValueTypes.FindIndex(kvp => kvp.Key == typeof(string));
						}
						List<string> options = defaultValueTypes.ConvertAll(kvp => kvp.Value);
						DropDownEvent.PopulateDropdown(valueType, options, this, SetEditType, defaultChoice, true);
						cHeader.columnSetting.type = defaultValueTypes[defaultChoice].Key;
					} else {
						DropDownEvent.PopulateDropdown(valueType, new string[] { sampleValueType.ToString() }, this, null, 0, false);
						cHeader.columnSetting.type = sampleValueType;
					}
					expectedValueType = sampleValueType;
				}
			}
		}
		public Type GetEditType() {
			List<object> editPath = cHeader.columnSetting.editPath;
			if (editPath == null || editPath.Count == 0) {
				return null;
			} else {
				object lastPathComponent = editPath[editPath.Count - 1];
				switch (lastPathComponent) {
				case FieldInfo fi: return fi.FieldType;
				case PropertyInfo pi: return pi.PropertyType;
				case string s: return typeof(object);
				}
			}
			return null;
		}
		private void SetEditType(int index) { cHeader.columnSetting.type = defaultValueTypes[index].Key; }
		private static List<KeyValuePair<Type, string>> defaultValueTypes = new List<KeyValuePair<Type, string>> {
			new KeyValuePair<Type, string>(typeof(object), "unknown"),
			new KeyValuePair<Type, string>(typeof(string), "string"),
			new KeyValuePair<Type, string>(typeof(double),"number"),
			new KeyValuePair<Type, string>(typeof(long),"integer"),
			new KeyValuePair<Type, string>(typeof(Token), "script"),
			new KeyValuePair<Type, string>(null, "read only"),
		};
		public void ColumnRemove() {
			ModalConfirmation ui = confirmRemoveUi;
			if (ui == null) { ui = Global.GetComponent<ModalConfirmation>(); }
			Udash.ColumnSetting cS = uds.GetColumn(column);
			ui.OkCancel("Are you sure you want to delete column \"" + cS.data.label + "\"?", () => {
				uds.RemoveColumn(column);
				Close();
			});
			uds.RefreshUi();
		}

		public void Close() {
			ClearInputTextBoxListeners();
			gameObject.SetActive(false);
		}
	}
}
