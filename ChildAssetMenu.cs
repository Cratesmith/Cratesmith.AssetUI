#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;

namespace cratesmith.assetui
{
	public static class SubAssetMenu 
	{
		[MenuItem("Assets/Delete Sub Asset(s) _DEL")]
		static void DeleteSubAsset()
		{
			var toDelete = Selection.objects.Where(CanEditSubAsset).ToList();
			
			if(!EditorUtility.DisplayDialog("Delete sub assets?",$"You're about to paremenantly delete the following {toDelete.Count} sub assets: {string.Join("\n-", toDelete)}", "Ok", "Cancel"))
				return;
			
			foreach (var subAsset in Selection.objects.Where(AssetDatabase.IsSubAsset))
			{
				AssetDatabase.RemoveObjectFromAsset(subAsset);
				Object.DestroyImmediate(subAsset,true);
			}
			AssetDatabase.SaveAssets();
		}
		
		[MenuItem("Assets/Delete Sub Asset(s) _DEL", validate = true)]
		static bool DeleteSubAssetValidate() => Selection.objects.Any(CanEditSubAsset);
		
		[MenuItem("Assets/Rename Sub Asset... _F2")]
		static void RenameSubAsset()
		{
			var target = Selection.activeObject;
			TextFieldPopupWindow.Create("Rename sub asset", newName =>
			{
				if (!target)
					return;
				
				target.name = newName;
				AssetDatabase.SaveAssets();
				EditorGUIUtility.PingObject(target); // this is a bit silly but it's the only easy way to force a refresh
			}, target.name, EditorIconUtility.GetIcon(Selection.activeObject));
		}
		
		[MenuItem("Assets/Rename Sub Asset... _F2", validate = true)]
		static bool RenameSubAssetValidate() => CanEditSubAsset(Selection.activeObject);

		static bool CanEditSubAsset(Object subAsset)
		{
			return subAsset 
			       && subAsset is ScriptableObject
			       && AssetDatabase.IsSubAsset(subAsset);

		}
	}
}
#endif