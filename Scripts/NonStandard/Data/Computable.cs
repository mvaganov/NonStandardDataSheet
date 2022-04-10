// code by michael vaganov, released to the public domain via the unlicense (https://unlicense.org/)
using NonStandard.Extension;
using System;
using System.Collections.Generic;
using System.Text;

namespace NonStandard.Data {
	/// <summary>
	/// handles variable dependencies and recursion analysis for variables/functions that reference other variables
	/// </summary>
	/// <typeparam name="KEY"></typeparam>
	/// <typeparam name="VAL"></typeparam>
	public class Computable<KEY,VAL> {
		/// <summary>
		/// name of this value
		/// </summary>
		public KEY _key;
		/// <summary>
		/// value
		/// </summary>
		public VAL _val;

		/// <summary>
		/// how this named value is computed
		/// </summary>
		private Func<VAL> compute;

		public Computable(KEY k, VAL v) { _key = k; _val = v; }

		public delegate void KeyValueChangeCallback(KEY Key, VAL oldValue, VAL newValue);

		/// <summary>
		/// callback whenever any change is made. onChange(oldValue, newValue)
		/// </summary>
		public KeyValueChangeCallback onChange;

		/// <summary>
		/// values that depend on this value. if this value changes, these need to be notified. we are the sunlight, these are the plant.
		/// </summary>
		public List<Computable<KEY, VAL>> dependents;


		/// <summary>
		/// values that this value relies on. if these values change, this needs to be notified. we are the plant, these are the sunlight.
		/// </summary>
		public List<Computable<KEY, VAL>> reliesOn;

		/// <summary>
		/// dirty flag, set when values this value relies on are changed. the sunlight told us it is changing, we need to adjust!
		/// </summary>
		private bool needsDependencyRecalculation = true;

		public KEY GetKey() => _key;

		/// <summary>
		/// the function used to compute this value. when set, the function is executed, and it's execution path is tested
		/// </summary>
		public Func<VAL> Compute {
			get => compute;
			set => SetCompute(value);
		}
		public KEY key { get => GetKey(); }
		public VAL value {
			get => GetValue();
			set {
				if (IsComputed || (_val == null && value != null) || !_val.Equals(value)) {
					SetCompute(null);
					UnityEngine.Debug.Log("setting "+key+" to "+value);
					VAL oldValue = _val;
					_val = value;
					if (onChange != null) onChange.Invoke(key, oldValue, _val);
				}
			}
		}

		// compute logic //////////////////////////////////////////////////

		private static Dictionary<System.Threading.Thread, List<Computable<KEY, VAL>>> pathNotes = 
			new Dictionary<System.Threading.Thread, List<Computable<KEY, VAL>>>();
		private static Dictionary<System.Threading.Thread, bool> watchingPaths = new Dictionary<System.Threading.Thread, bool>();
		public const int maxComputeDepth = 1000;

		private void FollowComputePath() {
			System.Threading.Thread t = System.Threading.Thread.CurrentThread;
			if (!watchingPaths.TryGetValue(t, out bool watchingPath)) { watchingPath = false; }
			if (!watchingPath) { return; }
			if (!pathNotes.TryGetValue(t, out List<Computable<KEY, VAL>> path)) {
				path = new List<Computable<KEY, VAL>>();
				pathNotes[System.Threading.Thread.CurrentThread] = path;
			}
			string err = null;
			if (path.Contains(this)) { err += "recursion"; }
			if (path.Count >= maxComputeDepth) { err += "max compute depth reached"; }
			if (!string.IsNullOrEmpty(err)) {
				throw new Exception(err + string.Join("->", path.ConvertAll(kv => kv._val.ToString()).ToArray()) + "~>" + GetValue());
			}
			needsDependencyRecalculation = true;
			path.Add(this);
		}

		public VAL GetValue() {
			if (watchingPaths.Count > 0) {
				FollowComputePath();
			}
			if (IsComputed && needsDependencyRecalculation) {
				SetInternal(compute.Invoke());
				needsDependencyRecalculation = false;
			}
			return _val;
		}

		/// <summary>
		/// hidden to the outside world so we can be sure parent listener/callbacks are called
		/// </summary>
		internal void SetInternal(VAL newValue) {
			if ((_val == null && newValue != null) || (_val != null && !_val.Equals(newValue))) {
				if (dependents != null) dependents.ForEach(dep => dep.needsDependencyRecalculation = true);
				VAL oldValue = _val;
				_val = newValue;
				if (onChange != null) onChange.Invoke(key, oldValue, newValue);
			}
		}


		/// <summary>
		/// if false, this is a simple value. if true, this value is calculated using a lambda expression
		/// </summary>
		public bool IsComputed => compute != null;
		public bool RemoveDependent(Computable<KEY, VAL> kv) {
			return (dependents != null) ? dependents.Remove(kv) : false;
		}
		public void AddDependent(Computable<KEY, VAL> kv) {
			if (dependents == null) { dependents = new List<Computable<KEY, VAL>>(); }
			dependents.Add(kv);
		}
		public void SetCompute(Func<VAL> value) {
			System.Threading.Thread t = System.Threading.Thread.CurrentThread;
			watchingPaths[t] = true;
			compute = value;
			List<Computable<KEY, VAL>> path = new List<Computable<KEY, VAL>>();
			pathNotes[t] = path;
			if (reliesOn != null) {
				reliesOn.ForEach(kv => kv.RemoveDependent(this));
				reliesOn.Clear();
			}
			_val = GetValue();
			if (reliesOn == null) {
				reliesOn = new List<Computable<KEY, VAL>>();
			}
			path.Remove(this);
			reliesOn.AddRange(path);
			reliesOn.ForEach(kv => kv.AddDependent(this));
			path.Clear();
			watchingPaths.Remove(t);
			pathNotes.Remove(t);
		}
		public override string ToString() { return key + ":" + value; }
		public object Printable(object o) {
			if (o is string s) { return "\"" + s.Escape() + "\""; }
			return o.StringifySmall();
		}
		public string ToString(bool showDependencies, bool showDependents) {
			StringBuilder sb = new StringBuilder();
			sb.Append(Printable(key)).Append(":").Append(Printable(value));
			if (showDependencies) { showDependencies = reliesOn != null && reliesOn.Count != 0; }
			if (showDependents) { showDependents = dependents != null && dependents.Count != 0; }
			if (showDependencies || showDependents) {
				sb.Append(" /*");
				if (showDependencies) {
					sb.Append(" relies on: ");
					//for(int i = 0; i < reliesOn.Count; ++i) { if(i>0) sb.Append(", "); sb.Append(reliesOn[i].key); }
					reliesOn.JoinToString(sb, ", ", r => r.key.ToString());
				}
				if (showDependents) {
					sb.Append(" dependents: ");
					//for (int i = 0; i < dependents.Count; ++i) { if (i > 0) sb.Append(", "); sb.Append(dependents[i].key); }
					dependents.JoinToString(sb, ", ", d => d.key.ToString());
				}
				sb.Append(" */");
			}
			return sb.ToString();
		}
	}
}
