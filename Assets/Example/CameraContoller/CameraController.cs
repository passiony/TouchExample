using System;
using System.Collections.Generic;
using Lean.Common;
using Lean.Touch;
using UnityEngine;
using UnityEngine.Serialization;

namespace Example
{
	[Serializable]
	public struct Area
	{
		public float xMin;
		public float xMax;
		public float yMin;
		public float yMax;
		public Area(float xMin, float xMax, float yMin, float yMax)
		{
			this.xMin = xMin;
			this.xMax = xMax;
			this.yMin = yMin;
			this.yMax = yMax;
		}
	}
	
	/// <summary>
	/// swipe 滑动范围限制
	/// </summary>
	[Serializable]
	public struct Areas
	{
		//最小fov的swipe范围
		public Area min;
		//最大fov的swipe范围
		public Area max;
		
		public Areas(Area min, Area max)
		{
			this.min = min;
			this.max = max;
		}

		public void Limit(ref Vector3 pos, float focusT, float elastic)
		{
			var xmin = Mathf.Lerp(min.xMin, max.xMin, focusT);
			var xmax = Mathf.Lerp(min.xMax, max.xMax, focusT);
			var ymin = Mathf.Lerp(min.yMin, max.yMin, focusT);
			var ymax = Mathf.Lerp(min.yMax, max.yMax, focusT);

			if (pos.x < xmin - elastic)
				pos.x = xmin - elastic;
			else if (pos.x > xmax + elastic)
				pos.x = xmax + elastic;

			if (pos.y < ymin - elastic)
				pos.y = ymin - elastic;
			else if (pos.y > ymax + elastic)
				pos.y = ymax + elastic;
		}
	}

	/// <summary>
	/// pinch 缩放的范围限制
	/// </summary>
	[Serializable]
	public struct Range
	{
		public float scaleMin;
		public float scaleMax;

		public Range(float scaleMin, float scaleMax)
		{
			this.scaleMin = scaleMin;
			this.scaleMax = scaleMax;
		}

		public void Limit(ref float value, float elastic)
		{
			if (value < scaleMin - elastic) value = scaleMin - elastic;
			else if (value > scaleMax + elastic) value = scaleMax + elastic;
		}
	}
	
	/// <summary>
	/// 使用LeanTouch控制，实现一个相机的拖拽和缩放的功能。pc端：swipe拖拽使用鼠标点击，pinch使用ctrl+鼠标
	/// 主要包含一下功能：
	/// 1.swipe和pinch的惯性实现
	/// 2.swipe和pinch的回弹效果
	/// </summary>
	public class CameraController : MonoBehaviour
	{
		/// <summary>
		/// //代表pinch的数值最小数量,如果LeanTouch的use hover打开，请设置为3。如果use hover关闭，请设置为2
		/// </summary>
		private const int PinchFingerCount = 2;
		private Camera camera;
		private Vector3 focusPos;
		private float focusSize;
		private Vector3 swipVelocity;
		
		private Vector3 currentPos;
		private float currentSize;
		
		//swipe区域
		public Areas swipAreas = new Areas(new Area(-40,40,-30,30), new Area(-20,20,-10,10));
		public float swipSensitivity = 0.1f;//swipe拖拽灵敏度
		public float swipBack = 20;//swipe最大回弹范围
		public float swipDamping = 10;//swipe拖拽的惯性阻尼

		//pinch区域
		public Range pinchRange = new Range(30, 50);
		public float pinchSensitivity = 1;//pinch的灵敏度
		public float pinchBack = 10;//pinch的最大回弹范围
		public float pinthDamping = 10;//pinch的惯性阻尼

		private void Awake()
		{
			camera = Camera.main;
			focusPos = camera.transform.position;
			focusSize = camera.fieldOfView;

			currentPos = camera.transform.position;
			currentSize = camera.fieldOfView;
		}

		protected virtual void OnEnable()
		{
			// Hook into the events we need
			LeanTouch.OnFingerUpdate += HandleFingerUpdate;
			LeanTouch.OnGesture      += HandleGesture;
		}

		protected virtual void OnDisable()
		{
			// Unhook the events
			LeanTouch.OnFingerUpdate -= HandleFingerUpdate;
			LeanTouch.OnGesture      -= HandleGesture;
		}

		//单指拖拽控制
		public void HandleFingerUpdate(LeanFinger finger)
		{
			var count = LeanTouch.Fingers.Count;
			if (count >= PinchFingerCount || count < PinchFingerCount - 1)
			{
				return;
			}

			SetMoveVelocity(finger.ScreenDelta);
		}
		
		//多指pinch控制
		public void HandleGesture(List<LeanFinger> fingers)
		{
			if (LeanTouch.Fingers.Count < PinchFingerCount)
			{
				return;
			}

			var pinchScale = LeanGesture.GetPinchScale(fingers);
			if (pinchScale != 1.0f)
			{
				pinchScale = Mathf.Pow(pinchScale, pinchSensitivity);
				focusSize /= pinchScale;
				pinchRange.Limit(ref focusSize, pinchBack);
			}
			
			
			var average = Vector2.zero;
			foreach (var finger in fingers)
			{
				average += finger.ScreenDelta;
			}
			SetMoveVelocity(average);
		}

		void SetMoveVelocity(Vector2 delta)
		{
			swipVelocity.x = delta.x;
			swipVelocity.y = delta.y;
			
			focusPos -= swipSensitivity * GetSensitive() * swipVelocity;
			swipAreas.Limit(ref focusPos, GetPinchT(), swipBack);
		}

		float GetSensitive()
		{
			return Mathf.Clamp01(focusSize / pinchRange.scaleMax);
		}
		
		float GetPinchT()
		{
			var t = (focusSize - pinchRange.scaleMin) / (pinchRange.scaleMax - pinchRange.scaleMin);
			return t;
		}
		
		private void Update()
		{
			if (LeanTouch.Fingers.Count == 0)
			{
				//手指松开后，回弹逻辑
				swipAreas.Limit(ref focusPos, GetPinchT(),0);
				pinchRange.Limit(ref focusSize, 0);
			}
			
			var factor1 = LeanHelper.GetDampenFactor(swipDamping, Time.deltaTime);
			currentPos = Vector2.Lerp(currentPos, focusPos, factor1);
			
			var factor2 = LeanHelper.GetDampenFactor(pinthDamping, Time.deltaTime);
			currentSize = Mathf.Lerp(currentSize, focusSize, factor2);
			
			camera.transform.position = currentPos;
			camera.fieldOfView = currentSize;
		}
	}
}