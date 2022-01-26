using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace cratesmith.assetui
{
    public static class EditorIconUtility
    {
        static Dictionary<Type, MonoScript> s_scriptLookup;

        public static Texture2D GetIcon(Type forScriptType)
        {
            if (s_scriptLookup == null)
            {
                s_scriptLookup = new Dictionary<Type, MonoScript>();
                var query = MonoImporter.GetAllRuntimeMonoScripts()
                    .Select(x => (type: x.GetClass(), script: x))
                    .Where(x => x.type != null)
                    .Distinct();
                
                // handle duplicate case (yes it actually can happen)
                foreach ((Type type, MonoScript script) x in query)
                {
                    s_scriptLookup[x.type] = x.script;
                }
            }

            return (Texture2D)EditorGUIUtility.ObjectContent(s_scriptLookup.TryGetValue(forScriptType, out var script) ? script:null, forScriptType)?.image;
        }
        
        public static Texture2D GetIcon(Object forObject)
        {
            if (forObject == null)
            {						
                return null;
            }

            if (forObject is ScriptableObject || forObject is MonoBehaviour || forObject is GameObject || forObject is MonoScript)
            {
            #if UNITY_2021_1_OR_NEWER
                var icon = EditorGUIUtility.GetIconForObject(forObject);
                if (icon) return icon;
            #else
                var ty = typeof(EditorGUIUtility);
                var mi = ty.GetMethod("GetIconForObject", BindingFlags.NonPublic | BindingFlags.Static);
                var icon = mi.Invoke(null, new object[] { forObject }) as Texture2D;
                if(icon) return icon;
            #endif
            }
    
            if (forObject is ScriptableObject || forObject is MonoBehaviour)
            {
                return GetIcon(forObject.GetType());
            }

            if (forObject is MonoScript ms)
            {
                return GetIcon(ms.GetType());
            }
            
            return (Texture2D)EditorGUIUtility.ObjectContent(forObject, forObject.GetType()).image;
        }
        
        public static void SetIcon(Object forObject, Texture2D iconTexture)
        {
            var ty = typeof(EditorGUIUtility);
            var mi2 = ty.GetMethod("SetIconForObject", BindingFlags.NonPublic | BindingFlags.Static);
            mi2.Invoke(null, new object[] { forObject, iconTexture });               
        }
    }
}