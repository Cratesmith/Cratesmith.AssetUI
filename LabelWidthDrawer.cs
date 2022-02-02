#if UNITY_EDITOR
using cratesmith.assetui;
using UnityEditor;
#endif
using UnityEngine;

namespace Cratesmith.AssetUI
{
	public class LabelWidthPropertyAttribute : PropertyAttribute
	{
		public readonly float depthStep;
		public readonly float widthScale;
		public readonly float maxWidth;
		public readonly float minWidth;
		
		public LabelWidthPropertyAttribute(float widthScale = .45f, float depthStep = -15f, float maxWidth = 500f, float minWidth = 0f)
		{
			this.widthScale = widthScale;
			this.depthStep = depthStep;
			this.maxWidth = maxWidth;
			this.minWidth = minWidth;
		}

#if UNITY_EDITOR
		public float GetLabelWidth(Rect position, SerializedProperty property)
		{
			return Mathf.Min(maxWidth, property.depth*depthStep + widthScale*(position.width-minWidth) + minWidth);
		}
#endif
	}
	
	#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(LabelWidthPropertyAttribute), true)]
	public class LabelWidthDrawer : PropertyDrawer
	{
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return EditorGUI.GetPropertyHeight(property, GUIContent.none, true);
		}	

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var prevLabelWdith = EditorGUIUtility.labelWidth;

			var attrib = (LabelWidthPropertyAttribute)attribute;
			EditorGUIUtility.labelWidth = attrib.GetLabelWidth(position,property);
			
			if (property.propertyType == SerializedPropertyType.ObjectReference)
			{
				ObjectDrawer.ObjectField(position, property, fieldInfo.FieldType.GetElementType() ?? fieldInfo.FieldType, label);
			} else
			{
				EditorGUI.PropertyField(position, property, label, true);
			}
			EditorGUIUtility.labelWidth = prevLabelWdith;
		}
	}
	#endif
}
