# Cratesmith.AssetUI
Popout Inspector and ScriptableObject aware default Object Drawer and other workflow tools for Unity3d.

# Installation:
![installation](https://user-images.githubusercontent.com/4616107/175195512-48ccefc4-63c4-4e15-80d6-9694f84428db.gif)

1. Open the package manager window
2. Click the add button in the top left
3. Select "add from git url"
4. Paste in the following url and press enter: https://github.com/Cratesmith/Cratesmith.AssetUI.git
5. ... or just check out the files and dump them somewhere in your project.
   
# Features

#### Eyedrop from scene
Enables an eyedrop tool that lets you pick compatible objects from the scene.

![Pick from scene](https://user-images.githubusercontent.com/4616107/175191256-b0f77a0d-7e1a-4b99-ba10-f1e5757317c3.gif)


#### Pick from Project
Shows a searchable list of compatible root-level components from prefabs in your project. This can have a brief delay in larger projects, especially for inbuilt types (such as Rigidbody)
![assetui_create_pick_from_project](https://user-images.githubusercontent.com/4616107/175192298-16bdf4dc-5f65-4e9d-8395-6362701b09fa.gif)

#### Create ScriptableObjects 
This option appears for any ScriptableObject type that has the [[CreateAssetMenu]] attribute.

![Create scriptable objects](https://user-images.githubusercontent.com/4616107/175192211-60ff2baf-7870-4586-b115-a08a9ebaf211.gif)


#### Create sub-assets of ScriptableObjects
You can create ScriptableObjects as sub-assets of the current ScriptableObject you're editing. This can be useful for reducing clutter or packing settings assets together
![create scriptableobject sub-assets](https://user-images.githubusercontent.com/4616107/175194506-796c768d-8192-4b10-8d3c-84a724d36edf.gif)


You can also rename and delete them as you would regular assets (f2 and del keys)
![assetui_rename_delete_sub_so_assets](https://user-images.githubusercontent.com/4616107/175194512-540ba430-49d4-4821-94f4-ad8fe327872b.gif)


#### Popout temporary inspector
Opens a temporary inspector. This inspector will close if it loses focus or you press escape. If you want to them around, either dock them or click the "pin" toggle button (just under the asset's icon)

![popout inspectors](https://user-images.githubusercontent.com/4616107/175193259-232fb4a7-84e6-4e23-9ac4-3be4e45a1822.gif)


#### Expand ScriptableObjects as foldouts
ScriptableObject fields can be expanded. The blueish foldout and grey box indicates that the contents of the foldout are part of another asset.
Note: At time of writing there are some issues using reorderable lists within these foldouts. If you find you can't edit a list just open a popout and edit that asset directly

![expand scriptableobjects as foldouts](https://user-images.githubusercontent.com/4616107/175192957-29528732-d5e1-486d-a209-ccae0b111ecd.gif)
 

#### Alt+\ Search components on selected object
If you have an object selected and push alt+\ this will open a searchable list of components and objects. Selecting anything from this list will open a popout inspector pointed at your selection

![Alt+\ search components on selected object](https://user-images.githubusercontent.com/4616107/175192813-d29b5c28-65b0-4f81-a86d-b8cb53e50b16.gif)


#### Ctrl+t Search for gameobject/component in prefabs in scene or project
Pressing ctrl+t opens a search window that lets you pick from gameobjects in open scenes or prefabs in your project. Selecting an object lets you search through components or gameobjects belonging to that (similar to alt+/) and open a popout inspector to edit them.
Note: holding shift when selecting will ping the object, holding ctrl when selecting will make that object your current selection (for the heirachy/project/inspector windows)

![assetui_ctrl_t_go_to_gameobject_or_prefab_component](https://user-images.githubusercontent.com/4616107/175194740-5d0c16ad-4600-4830-b5dc-81674e36c039.gif)

