using UnityEngine;

namespace NonStandard.Ui {
	public static class DirectionExtensionUnity {
		public static Vector2 GetVector2(this Direction2D d, bool normalized = true) {
			Vector2 v = Vector2.zero;
			if (d.HasFlag(Direction2D.Left)) v += Vector2.left;
			if (d.HasFlag(Direction2D.Right)) v += Vector2.right;
			if (d.HasFlag(Direction2D.Top)) v += Vector2.up;
			if (d.HasFlag(Direction2D.Bottom)) v += Vector2.down;
			if (normalized) { v.Normalize(); }
			return v;
		}
		public static Vector3 GetVector3(this Direction3D d, bool normalized = true) {
			Vector3 v = Vector3.zero;
			if (d.HasFlag(Direction3D.Forward)) v += Vector3.forward;
			if (d.HasFlag(Direction3D.Back)) v += Vector3.back;
			if (d.HasFlag(Direction3D.Left)) v += Vector3.left;
			if (d.HasFlag(Direction3D.Right)) v += Vector3.right;
			if (d.HasFlag(Direction3D.Up)) v += Vector3.up;
			if (d.HasFlag(Direction3D.Down)) v += Vector3.down;
			if (normalized) { v.Normalize(); }
			return v;
		}
	}
}
