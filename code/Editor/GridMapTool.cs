﻿using Editor.Widgets;
using RogueFPS;
using Sandbox;
using Sandbox.UI;
using System;
using static Sandbox.HitboxSet;

namespace Editor;

/// <summary>
/// An editor tool for placing models on a grid.
/// </summary>
[EditorTool]
[Title( "Grid Map Tool" )]
[Icon( "🏗️" )]
[Group( "Grid Map Editor" )]
[Order( 0 )]
public class GridMapTool : EditorTool
{
	public GameObject SelectedObject { get; set; }

	public string CopyString { get; set; }

	public GridMapEditorResource resource { get; set; }

	public enum ModelPrefabSelection
	{
		Model,
		Prefab
	}
	public ModelPrefabSelection CurrentSelection { get; set; } = ModelPrefabSelection.Model;

	public string SearchString { get; set; } = "";

	public ListView modellistView { get; set; } = new();
	public ListView prefablistView { get; set; } = new();

	public List<PrefabFile> prefabList { get; set; } = new();
	public List<ModelList> modelList { get; set; } = new();

	public struct ModelList
	{
		public string name;
		public string path;
		public Pixmap icon;
	}

	public PrefabFile SelectedPrefab { get; set; }
	public string SelectedModel { get; set; }

	public float floors = 0.0f;
	public int floorCount = 0;

	public Rotation rotation = Rotation.From( 90, 0, 0 );
	public enum RotationSnap
	{
		None = 0,
		Five = 5,
		Fifteen = 15,
		FortyFive = 45,
		Ninety = 90
	}
	public RotationSnap CurrentRotationSnap { get; set; } = RotationSnap.Ninety;

	public enum PaintMode
	{
		Place = 0,
		Remove = 1,
		Move = 2,
		Copy = 3,
	}
	public PaintMode CurrentPaintMode { get; set; } = PaintMode.Place;

	public GameObject CurrentGameObjectCollection;
	public List<GameObject> GameObjectCollection { get; set; } = new();

	public ComboBox collectionDropDown { get; set; } = new();
	
	public override void OnEnabled()
	{
		var so = EditorTypeLibrary.GetSerializedObject( this );
		AllowGameObjectSelection = false;

		foreach (var objectinscene in Scene.GetAllObjects(true))
		{
			if ( objectinscene.Tags.Has( "Collection" ) && (objectinscene.Parent is Scene) )
			{
				collectionDropDown.AddItem( objectinscene.Name );
				GameObjectCollection.Add( objectinscene );
			}
		}

		{
			var window = new TileWindow( SceneOverlay, Scene );
			window.MaximumWidth = 300;

			var rotshort = new Shortcut( SceneOverlay, "Rotate", "p", () => Log.Info( "Buttes" ) );
			var row = Layout.Column();
			var cs = new ControlSheet();
			var paintmode = row.Add( new SegmentedControl() );
			paintmode.AddOption( "Place", "brush" );
			paintmode.AddOption( "Remove", "delete" );
			paintmode.AddOption( "Move", "open_with" );
			paintmode.AddOption( "Copy", "content_copy" );


			paintmode.OnSelectedChanged += ( s ) =>
			{
				CurrentPaintMode = Enum.Parse<PaintMode>( s );
			};

			cs.AddRow( so.GetProperty( "resource" ) );

			var collectionrow = Layout.Row();
			/*var search = new LineEdit();
			search.MinimumHeight = Theme.RowHeight;
			search.PlaceholderText = "Search...";
			search.TextEdited += ( s ) =>
			{
				SearchString = s;
			};
			*/

			var search = new LineEdit();
			search.MinimumHeight = Theme.RowHeight;
			search.PlaceholderText = "Search...";
			search.TextEdited += OnSearchTextChanged;
			
			var collectionLabel = new Label( "Collection" );
			var collectionCombo = collectionDropDown;
			collectionrow.AddSpacingCell( 5 );

			var collectionButton = new Button( "", "add" );
			collectionButton.ButtonType = "clear";
			collectionButton.Clicked = () =>
			{
				var popup = new NewCollectionObjectWindow();
				popup.Show();
				popup.gridmapToolDockWidget = this;
			};
			collectionButton.MaximumWidth = 32;

			collectionrow.Add( search );
			collectionrow.AddSpacingCell( 5 );
			collectionrow.Add( collectionLabel );
			collectionrow.AddSpacingCell( 5 );
			collectionrow.Add( collectionCombo );
			collectionrow.Add( collectionButton );


			var modelprefab = row.Add( new SegmentedControl() );
			modelprefab.AddOption( "Model", "brush" );
			modelprefab.AddOption( "Prefab", "delete" );
			modelprefab.OnSelectedChanged += ( s ) =>
			{
				CurrentSelection = Enum.Parse<ModelPrefabSelection>( s, true );
				UpdateListViewVisibility();
				UpdateListViewItems();
			};

			prefablistView.ItemSize = 64;
			prefablistView.ItemAlign = Sandbox.UI.Align.SpaceBetween;
			prefablistView.OnPaintOverride += () => PaintListBackground( prefablistView );
			prefablistView.ItemPaint = PaintBrushPrefab;
			prefablistView.ItemSelected = ( item ) =>
			{
				if ( item is PrefabFile data )
				{
					SelectedPrefab = data;
				}
			};

			modellistView.ItemSize = new Vector2( 64, 64 );
			modellistView.ItemAlign = Sandbox.UI.Align.SpaceBetween;
			modellistView.ItemSelected = ( item ) =>
			{
				if ( item is ModelList data )
				{
					SelectedModel = data.path;
				}
			};

			modellistView.OnPaintOverride += () => PaintListBackground( modellistView );
			modellistView.ItemPaint = PaintBrushItem;
			row.Add( collectionrow );
			row.Add( cs );
			row.Add( modellistView );
			row.Add( prefablistView );

			window.Layout = row;
			window.MinimumSize = new Vector2( 300, 600 );

			AddOverlay( window, TextFlag.RightBottom, 10 );
		}
		UpdateListViewVisibility();
		UpdateListViewItems();
		{
			var window = new WidgetWindow( SceneOverlay, "Grid Map Controls" );
			window.MaximumWidth = 300;
			var cs = new ControlSheet();
			cs.AddRow( so.GetProperty( "CurrentRotationSnap" ) );
			var Rotbuttons = Layout.Row();
			Rotbuttons.Spacing = 8;
			Rotbuttons.Add( new Label( "Rotation:" ) );
			Rotbuttons.AddStretchCell( 1 );
			Rotbuttons.Add( new Button( "", "arrow_back" ) { Clicked = DoRotation( true ) } );
			Rotbuttons.Add( new Button( "", "arrow_forward" ) { Clicked = DoRotation( false ) } );

			var Floorbuttons = Layout.Row();
			Floorbuttons.Spacing = 8;
			Floorbuttons.Add( new Label( "Floor:" ) );
			Floorbuttons.AddStretchCell( 1 );
			var floorLabel = new Label( floorCount.ToString() );
			Floorbuttons.Add( floorLabel );
			Floorbuttons.AddStretchCell( 1 );
			Floorbuttons.Add( new Button( "", "arrow_upward" ) { Clicked = () => { DoFloors( 128 )(); floorLabel.Text = floorCount.ToString(); } } );
			Floorbuttons.Add( new Button( "", "arrow_downward" ) { Clicked = () => { DoFloors( -128 )(); floorLabel.Text = floorCount.ToString(); } } );
			cs.AddLayout( Floorbuttons );


			cs.AddLayout( Rotbuttons );
			cs.AddLayout( Floorbuttons );

			//cs.Add( new Button( "Clear", "clear" ) { Clicked = ClearAll } );
			window.Layout = cs;


			AddOverlay( window, TextFlag.RightTop, 10 );
		}

	}

	private void OnSearchTextChanged( string searchText )
	{
		SearchString = searchText.ToLower();
		UpdateListViewItems();
	}


	private void UpdateListViewItems()
	{
		if ( CurrentSelection == ModelPrefabSelection.Model )
		{
			var filteredModelList = modelList
				.Where( model => model.name.ToLower().Contains( SearchString ) )
				.ToList();
			modellistView.SetItems( filteredModelList.Cast<object>() );
			modellistView.Update(); // Refresh ListView
		}
		else if ( CurrentSelection == ModelPrefabSelection.Prefab )
		{
			var filteredPrefabList = prefabList
				.Where( prefab => prefab.ResourceName.ToLower().Contains( SearchString ) )
				.ToList();
			prefablistView.SetItems( filteredPrefabList.Cast<object>() );
			prefablistView.Update(); // Refresh ListView
		}
	}

	private void UpdateListViewVisibility()
	{
		modellistView.Visible = CurrentSelection == ModelPrefabSelection.Model;
		prefablistView.Visible = CurrentSelection == ModelPrefabSelection.Prefab;
	}


	public override void OnDisabled()
	{
		base.OnDisabled();
	}

	private Vector3 startSelectionPoint;
	private Vector3 endSelectionPoint;
	private bool isSelecting = false;

	private void FillSelectionWithTiles( Vector3 start, Vector3 end, SceneTraceResult tr )
	{
		float gridSpacing = Gizmo.Settings.GridSpacing;
		Vector3 lowerCorner = new Vector3( Math.Min( start.x, end.x ), Math.Min( start.y, end.y ), start.z ); // Use start.z as it's the ground level
		Vector3 upperCorner = new Vector3( Math.Max( start.x, end.x ), Math.Max( start.y, end.y ), start.z );

		for ( float x = lowerCorner.x; x <= upperCorner.x; x += gridSpacing )
		{
			for ( float y = lowerCorner.y; y <= upperCorner.y; y += gridSpacing )
			{
				PlaceTileAtPosition( new Vector3( x, y, lowerCorner.z ), tr );
			}
		}
	}

	private void PlaceTileAtPosition( Vector3 position, SceneTraceResult tr )
	{
		if ( SelectedModel != null )
		{
			var go = new GameObject( true, "GridTile" );
			go.Components.Create<ModelRenderer>().Model = Model.Load( SelectedModel );
			go.Components.Create<ModelCollider>().Model = Model.Load( SelectedModel );
			go.Transform.Position = position;
			go.Transform.Rotation = Rotation.LookAt( tr.Normal ) * rotation;
			go.Tags.Add( "sprinkled" );
		}
	}

	//GPT Moment
	private SceneTraceResult ProjectRayOntoGroundPlane( Vector3 rayOrigin, Vector3 rayDirection, float groundHeight )
	{
		Vector3 planeNormal = Vector3.Up;
		Vector3 planePoint = new Vector3( 0, 0, groundHeight );

		// Correct usage of Vector3.Dot based on your environment's API
		float denom = Vector3.Dot( planeNormal, rayDirection );
		if ( Math.Abs( denom ) > 0.0001f ) // Ensure not parallel
		{
			float num = Vector3.Dot( planeNormal, planePoint );
			float distance = (num - Vector3.Dot( planeNormal, rayOrigin )) / denom;

			if ( distance >= 0 )
			{
				// Calculate the hit point
				Vector3 hitPoint = rayOrigin + rayDirection * distance;
				return new SceneTraceResult { Hit = true, EndPosition = hitPoint };
			}
		}

		return new SceneTraceResult { Hit = false };
	}

	List<GameObject> SelectedGroupObjects { get; set; } = new List<GameObject>();

	private void SelectObjectsInBox( BBox selectionBox )
	{

		foreach ( var obj in Scene.GetAllObjects( false ) )
		{
			if ( selectionBox.Contains( obj.Transform.Position ) && obj.Tags.Has( "sprinkled" ) )
			{
				SelectedGroupObjects.Add( obj );
			}
		}
	}


	public override void OnUpdate()
	{

		if ( GameObjectCollection is not null )
		{
			CurrentGameObjectCollection = GameObjectCollection.FirstOrDefault( x => x.Name == collectionDropDown.CurrentText );
		}

		if ( resource != null )
		{
			foreach ( var model in resource.TileModels )
			{
				if ( !modelList.Any( x => x.name == model.ResourceName ) )
				{
					modelList.Add( new ModelList()
					{
						name = model.ResourceName,
						path = model.ResourcePath,
						icon = AssetSystem.FindByPath( model.ResourcePath ).GetAssetThumb()
					} );

					Log.Info( model.Name );
				}
			}

			foreach ( var prefab in resource.TilePrefab )
			{
				if ( !prefabList.Any( x => x.ResourceName == prefab.ResourceName ) )
				{
					prefabList.Add( prefab );

					Log.Info( prefab.ResourceName );
				}
			}
			UpdateListViewItems();
			//modellistView.SetItems( modelList.Cast<object>() );

			//prefablistView.SetItems( prefabList.Cast<object>() );
		}

		modellistView.ItemsSelected = SetSelection;

		// Do gizmos and stuff
		var cursorRay = Gizmo.CurrentRay;

		var tr = Scene.Trace.Ray( cursorRay, 5000 )
						.UseRenderMeshes( true )
						.UsePhysicsWorld( false )
						.WithoutTags( "sprinkled" )
						.Run();

		if ( resource is null )
			return;

		if ( Gizmo.IsShiftPressed )
		{
			var boxtr = Scene.Trace.Ray(cursorRay,5000)
						.UsePhysicsWorld( true )
						.WithoutTags( "sprinkled" )
						.Run();

			if ( !boxtr.Hit )
			{
				Vector3 rayOrigin = cursorRay.Position;
				Vector3 rayDirection = cursorRay.Forward;

				boxtr = ProjectRayOntoGroundPlane( rayOrigin, rayDirection, 0 );
			}

			if ( Gizmo.WasLeftMousePressed )
			{
				startSelectionPoint = boxtr.EndPosition.WithZ(floors).SnapToGrid( Gizmo.Settings.GridSpacing );
				isSelecting = true;
			}
			else if ( isSelecting )
			{
				endSelectionPoint = boxtr.EndPosition.WithZ( floors + 128 ).SnapToGrid( Gizmo.Settings.GridSpacing );

				if ( Gizmo.WasLeftMouseReleased )
				{
					var bbox = new BBox( startSelectionPoint, endSelectionPoint );
					SelectObjectsInBox( bbox );
					isSelecting = false;
				}
			}

			if ( Application.IsKeyDown( KeyCode.F ) )
			{
				FillSelectionWithTiles( startSelectionPoint, endSelectionPoint, tr );
				isSelecting = false;
			}

			if ( Application.IsKeyDown( KeyCode.X ) )
			{
				foreach ( var obj in SelectedGroupObjects )
				{
					obj.Destroy();
				}
			}

			if( SelectedGroupObjects is not null)
			{
				using ( Gizmo.Scope( "Selection" ) )
				{
					foreach ( var obj in SelectedGroupObjects )
					{
						Gizmo.Draw.Color = Color.Red;
						Gizmo.Draw.LineBBox( obj.GetBounds() );
					}
				}
			}
			using ( Gizmo.Scope( "selection_box" ) )
			{
				var rect = new BBox( startSelectionPoint, endSelectionPoint );
				Gizmo.Draw.Color = Color.Blue.WithAlpha( 0.25f );
				Gizmo.Draw.SolidBox( rect );
				Gizmo.Draw.Color = Color.Blue;
				Gizmo.Draw.LineBBox( rect );
			}

			return;
		}
		else if ( isSelecting )
		{	
			isSelecting = false;
		}
		else
		{
			if ( SelectedGroupObjects != null )
			{
				SelectedGroupObjects.Clear();
			}
			startSelectionPoint = Vector3.Zero;
			endSelectionPoint = Vector3.Zero;
		}

		if ( Gizmo.IsCtrlPressed && Gizmo.WasLeftMousePressed )
		{
			rotation *= Rotation.FromYaw( (float)CurrentRotationSnap );
		}

		if ( tr.Hit )
		{
			if ( CurrentSelection == ModelPrefabSelection.Model )
			{
				PaintModelGizmos( tr );
				if ( previewPrefab is not null )
				{
					previewPrefab.Destroy();
					previewPrefab = null;
				}
			}
			else if ( CurrentSelection == ModelPrefabSelection.Prefab )
			{
				PaintPrefabGizmos( tr );
			}
		}


		if ( !Gizmo.IsCtrlPressed && CurrentSelection == ModelPrefabSelection.Model )
		{
			if ( Gizmo.WasLeftMousePressed && CurrentPaintMode == PaintMode.Place )
			{
				var go = new GameObject( true, "GridTile" );
				go.Parent = CurrentGameObjectCollection;
				go.Components.Create<ModelRenderer>().Model = Model.Load( SelectedModel );
				go.Components.Create<ModelCollider>().Model = Model.Load( SelectedModel );
				go.Transform.Position = tr.HitPosition.SnapToGrid( Gizmo.Settings.GridSpacing ).WithZ( floors );
				go.Transform.Rotation = Rotation.LookAt( tr.Normal ) * rotation;
				go.Tags.Add( "sprinkled" );
			}
			else if ( Gizmo.WasLeftMousePressed && CurrentPaintMode == PaintMode.Remove )
			{
				var tr2 = Scene.Trace.Ray( cursorRay, 5000 )
								.UseRenderMeshes( true )
								.UsePhysicsWorld( false )
								.WithTag( "sprinkled" )
								.Run();
				if ( tr2.Hit )
				{
					tr2.GameObject.Destroy();
				}
			}

			if ( Gizmo.WasLeftMousePressed && CurrentPaintMode == PaintMode.Move )
			{
				var tr2 = Scene.Trace.Ray( cursorRay, 5000 )
								.UseRenderMeshes( true )
								.UsePhysicsWorld( false )
								.WithTag( "sprinkled" )
								.Run();
				if ( tr2.Hit )
				{
					SelectedObject = tr2.GameObject;
				}
			}
			else if ( Gizmo.WasLeftMouseReleased && CurrentPaintMode == PaintMode.Move && SelectedObject is not null )
			{
				SelectedObject = null;
			}
			if ( Gizmo.IsLeftMouseDown && CurrentPaintMode == PaintMode.Move && SelectedObject is not null )
			{
				SelectedObject.Transform.Position = tr.HitPosition.SnapToGrid( Gizmo.Settings.GridSpacing ).WithZ( floors );
				SelectedObject.Transform.Rotation = Rotation.LookAt( tr.Normal ) * rotation;
			}

			if ( Gizmo.WasLeftMouseReleased && CurrentPaintMode == PaintMode.Copy && CopyString == null )
			{
				var tr2 = Scene.Trace.Ray( cursorRay, 5000 )
								.UseRenderMeshes( true )
								.UsePhysicsWorld( false )
								.WithTag( "sprinkled" )
								.Run();
				if ( tr2.Hit )
				{
					CopyString = tr2.GameObject.Components.Get<ModelRenderer>().Model.Name;
				}
			}

			if ( Gizmo.WasLeftMousePressed && CurrentPaintMode == PaintMode.Copy && CopyString != null )
			{
				var go = new GameObject( true, "GridTile" );
				go.Components.Create<ModelRenderer>().Model = Model.Load( CopyString );
				go.Components.Create<ModelCollider>().Model = Model.Load( CopyString );
				go.Transform.Position = tr.HitPosition.SnapToGrid( Gizmo.Settings.GridSpacing ).WithZ( floors );
				go.Transform.Rotation = Rotation.LookAt( tr.Normal ) * rotation;
				go.Tags.Add( "sprinkled" );
			}

			if ( Application.IsKeyDown( KeyCode.Escape ) && CopyString != null )
			{
				CopyString = null;
			}
		}
		else if ( !Gizmo.IsCtrlPressed && CurrentSelection == ModelPrefabSelection.Prefab)
		{
			if ( Gizmo.WasLeftMousePressed && CurrentPaintMode == PaintMode.Place && previewPrefab != null )
			{
				var go = SceneUtility.Instantiate( SceneUtility.GetPrefabScene( SelectedPrefab ) );
				go.Transform.Position = tr.HitPosition.SnapToGrid( Gizmo.Settings.GridSpacing ).WithZ( floors );
				go.Transform.Rotation = Rotation.LookAt( tr.Normal ) * rotation;
				go.Tags.Add( "sprinkled" );
			}
			else if ( Gizmo.WasLeftMousePressed && CurrentPaintMode == PaintMode.Remove )
			{
				var tr2 = Scene.Trace.Ray( cursorRay, 5000 )
								.UseRenderMeshes( true )
								.UsePhysicsWorld( false )
								.WithTag( "sprinkled" )
								.Run();
				if ( tr2.Hit )
				{
					tr2.GameObject.Destroy();
				}
			}
		}


		if ( !Gizmo.IsLeftMouseDown )
			return;
	}

	GameObject previewPrefab;

	public void PaintPrefabGizmos( SceneTraceResult tr )
	{
		var cursorRay = Gizmo.CurrentRay;

		if ( CurrentPaintMode == PaintMode.Place && SelectedPrefab is not null )
		{
			if ( previewPrefab is null )
			{
				previewPrefab = SceneUtility.Instantiate( SceneUtility.GetPrefabScene( SelectedPrefab ) );
				previewPrefab.Tags.Add( "sprinkled" );
			}

			if ( previewPrefab != SceneUtility.GetPrefabScene( SelectedPrefab ) )
			{
				previewPrefab.Destroy();

				previewPrefab = SceneUtility.Instantiate( SceneUtility.GetPrefabScene( SelectedPrefab ) );
				previewPrefab.Tags.Add( "sprinkled" );
			}

			if ( previewPrefab is not null )
			{
				using ( Gizmo.Scope( "preview" ) )
				{
					Gizmo.Transform = new Transform( tr.HitPosition.SnapToGrid( Gizmo.Settings.GridSpacing ), Rotation.LookAt( tr.Normal ) * rotation );
					Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( 0.35f );
					Gizmo.Draw.SolidBox( previewPrefab.GetBounds() );
					Gizmo.Draw.LineBBox( previewPrefab.GetBounds() );

					Gizmo.Transform = new Transform( tr.HitPosition.SnapToGrid( Gizmo.Settings.GridSpacing ).WithZ( floors ), Rotation.LookAt( tr.Normal ) * rotation );
					previewPrefab.Transform.Position = tr.HitPosition.SnapToGrid( Gizmo.Settings.GridSpacing ).WithZ( floors );
					previewPrefab.Transform.Rotation = Rotation.LookAt( tr.Normal ) * rotation;
				}
			}
		}
		else if( CurrentPaintMode != PaintMode.Place)
		{
			if ( previewPrefab != null )
			{
				previewPrefab.Destroy();
				previewPrefab = null;
			}
		}

		if ( CurrentPaintMode == PaintMode.Remove )
		{
			var tr2 = Scene.Trace.Ray( cursorRay, 5000 )
					.UseRenderMeshes( true )
					.UsePhysicsWorld( false )
					.WithTag( "sprinkled" )
					.Run();
			
			if ( tr2.Hit )
			{
				Gizmo.Draw.Color = Color.Red.WithAlpha( 0.5f );
				Gizmo.Draw.LineBBox( tr2.GameObject.GetBounds() );
				Gizmo.Draw.SolidBox( tr2.GameObject.GetBounds() );
			}
		}
	}

	public void PaintModelGizmos( SceneTraceResult tr )
	{
		// Do gizmos and stuff
		var cursorRay = Gizmo.CurrentRay;

		if ( CurrentPaintMode == PaintMode.Place && SelectedModel is not null )
		{
			using ( Gizmo.Scope( "preview" ) )
			{

				Gizmo.Transform = new Transform( tr.HitPosition.SnapToGrid( Gizmo.Settings.GridSpacing ), Rotation.LookAt( tr.Normal ) * rotation );
				Gizmo.Draw.Color = Gizmo.Colors.Green.WithAlpha( 0.35f );
				Gizmo.Draw.SolidBox( Model.Load( SelectedModel ).Bounds );
				Gizmo.Draw.LineBBox( Model.Load( SelectedModel ).Bounds );
				Gizmo.Transform = new Transform( tr.HitPosition.SnapToGrid( Gizmo.Settings.GridSpacing ).WithZ( floors ), Rotation.LookAt( tr.Normal ) * rotation );

				//var trans = new Transform( tr.HitPosition, Rotation.LookAt( tr.Normal ) );
				Gizmo.Draw.Color = Color.White;
				Gizmo.Draw.Model( Model.Load( SelectedModel ) );
			}
		}
		else if ( CurrentPaintMode == PaintMode.Remove )
		{
			using ( Gizmo.Scope( "preview" ) )
			{
				var tr2 = Scene.Trace.Ray( cursorRay, 5000 )
						.UseRenderMeshes( true )
						.UsePhysicsWorld( false )
						.WithTag( "sprinkled" )
						.Run();
				if ( tr2.Hit )
				{
					Gizmo.Draw.Color = Color.Red.WithAlpha( 0.5f );
					Gizmo.Draw.LineBBox( tr2.GameObject.GetBounds() );
					Gizmo.Draw.SolidBox( tr2.GameObject.GetBounds() );
				}
			}
		}
		else if ( CurrentPaintMode == PaintMode.Move )
		{
			using ( Gizmo.Scope( "preview" ) )
			{
				var tr2 = Scene.Trace.Ray( cursorRay, 5000 )
						.UseRenderMeshes( true )
						.UsePhysicsWorld( false )
						.WithTag( "sprinkled" )
						.Run();
				if ( tr2.Hit && SelectedObject is null )
				{
					Gizmo.Draw.Color = Theme.Blue.WithAlpha( 0.5f );
					Gizmo.Draw.LineBBox( tr2.GameObject.GetBounds() );
					Gizmo.Draw.SolidBox( tr2.GameObject.GetBounds() );
				}
				else if ( SelectedObject is not null )
				{
					Gizmo.Draw.Color = Theme.Blue.WithAlpha( 0.5f );
					Gizmo.Draw.LineBBox( SelectedObject.GetBounds() );
					Gizmo.Draw.SolidBox( SelectedObject.GetBounds() );
				}
			}
		}
		else if ( CurrentPaintMode == PaintMode.Copy )
		{
			using ( Gizmo.Scope( "preview" ) )
			{
				var tr2 = Scene.Trace.Ray( cursorRay, 5000 )
						.UseRenderMeshes( true )
						.UsePhysicsWorld( false )
						.WithTag( "sprinkled" )
						.Run();
				if ( tr.Hit && CopyString != null )
				{

					Gizmo.Transform = new Transform( tr.HitPosition.SnapToGrid( Gizmo.Settings.GridSpacing ).WithZ( floors ), Rotation.LookAt( tr.Normal ) * rotation );
					Gizmo.Draw.Color = Color.Yellow.WithAlpha( 0.15f );
					Gizmo.Draw.LineBBox( Model.Load( CopyString ).Bounds );
					Gizmo.Draw.SolidBox( Model.Load( CopyString ).Bounds );

					Gizmo.Draw.Color = Color.White;
					Gizmo.Draw.Model( Model.Load( CopyString ) );
				}
				else if ( tr2.Hit && CopyString == null )
				{
					Gizmo.Draw.Color = Theme.Yellow.WithAlpha( 0.5f );
					Gizmo.Draw.LineBBox( tr2.GameObject.GetBounds() );
					Gizmo.Draw.SolidBox( tr2.GameObject.GetBounds() );
				}
			}
		}
	}

	private static bool PaintListBackground( Widget widget )
	{
		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground.WithAlpha( 0.5f ) );
		Paint.DrawRect( widget.LocalRect );

		return false;
	}

	/// <summary>
	/// UI Paint
	/// </summary>
	/// <param name="widget"></param>
	private void PaintBrushItem( VirtualWidget widget )
	{
		var brush = (ModelList)widget.Object;

		Paint.Antialiasing = true;
		Paint.TextAntialiasing = true;


		if ( brush.path == SelectedModel )
		{
			Paint.ClearPen();
			Paint.SetBrush( Theme.Green.WithAlpha( 0.10f ) );
			Paint.SetPen( Theme.Green.WithAlpha( 0.50f ) );
			Paint.DrawRect( widget.Rect.Grow( 0 ), 3 );
		}

		Paint.ClearPen();
		Paint.SetBrush( Theme.White.WithAlpha( 0.01f ) );
		Paint.SetPen( Theme.White.WithAlpha( 0.05f ) );
		Paint.DrawRect( widget.Rect.Shrink( 2 ), 3 );

		Paint.Draw( widget.Rect.Shrink( 1 ), brush.icon );

		var rect = widget.Rect;

		var textRect = rect.Shrink( 2 );
		textRect.Top = textRect.Top + 20;
		textRect.Top = textRect.Top + 25;

		Paint.ClearPen();
		Paint.SetBrush( Theme.Black.WithAlpha( 0.5f ) );
		Paint.DrawRect( textRect, 0.0f );

		Paint.Antialiasing = true;

		Paint.SetPen( Theme.Blue, 2.0f );
		Paint.ClearBrush();
		Paint.SetFont( "Poppins", 6, 700 );
		Paint.DrawText( textRect, brush.name );
	}
	private void PaintBrushPrefab( VirtualWidget widget )
	{
		var brush = (PrefabFile)widget.Object;

		Paint.Antialiasing = true;
		Paint.TextAntialiasing = true;


		if ( brush == SelectedPrefab )
		{
			Paint.ClearPen();
			Paint.SetBrush( Theme.Green.WithAlpha( 0.10f ) );
			Paint.SetPen( Theme.Green.WithAlpha( 0.50f ) );
			Paint.DrawRect( widget.Rect.Grow( 0 ), 3 );
		}

		Paint.ClearPen();
		Paint.SetBrush( Theme.White.WithAlpha( 0.01f ) );
		Paint.SetPen( Theme.White.WithAlpha( 0.05f ) );
		Paint.DrawRect( widget.Rect.Shrink( 2 ), 3 );

		Paint.Draw( widget.Rect.Shrink( 1 ), AssetSystem.FindByPath( brush.ResourcePath ).GetAssetThumb() );

		var rect = widget.Rect;

		var textRect = rect.Shrink( 2 );
		textRect.Top = textRect.Top + 20;
		textRect.Top = textRect.Top + 25;

		Paint.ClearPen();
		Paint.SetBrush( Theme.Black.WithAlpha( 0.5f ) );
		Paint.DrawRect( textRect, 0.0f );

		Paint.Antialiasing = true;

		Paint.SetPen( Theme.Blue, 2.0f );
		Paint.ClearBrush();
		Paint.SetFont( "Poppins", 6, 700 );
		Paint.DrawText( textRect, brush.ResourceName );
	}


	void SetSelection( object o )
	{
		if ( o is string s )
		{
			SelectedModel = s;

			Log.Info( $"Selected {s}" );
		}
	}

	private Action DoRotation( bool leftright )
	{

		Log.Info( "Rotate" );

		return () =>
		{
			if ( leftright )
			{
				rotation *= Rotation.FromYaw( (float)CurrentRotationSnap );
			}
			else
			{
				rotation *= Rotation.FromYaw( -(float)CurrentRotationSnap );
			}
		};

	}

	private Action DoFloors( float i )
	{
		return () =>
		{
			floors += i;
			if ( i > 0 )
			{
				floorCount++;
			}
			else if ( i < 0 )
			{
				floorCount--;
			}
		};
	}

	public void CreateCollection( string name )
	{
		using var scope = SceneEditorSession.Scope();
		var go = new GameObject( true, name );
		go.Transform.Position = Vector3.Zero;
		go.Tags.Add( "collection" );
		go.Tags.Add( "sprinkled" );

		GameObjectCollection.Add( go );

		Log.Info( "Created collection" );
	}

	void ClearAll()
	{
		foreach ( var obj in Scene.GetAllObjects( false ).Where( x => x.Tags.Has( "sprinkled" ) ).ToArray() )
		{
			obj.Destroy();
		}
	}

}

file class TileWindow : WidgetWindow
{
	Scene scene;
	public TileWindow( Widget parent, Scene scene ) : base( parent, "Grid Map Editor" )
	{

		this.scene = scene;
	}

	protected override void OnPaint()
	{
		base.OnPaint();

	}
}
public class NewCollectionObjectWindow : BaseWindow
{

	Widget container;
	public GridMapTool gridmapToolDockWidget;

	public NewCollectionObjectWindow()
	{
		Size = new Vector2( 350, 150 );
		MinimumSize = Size;
		MaximumSize = Size;
		TranslucentBackground = true;
		NoSystemBackground = true;

		WindowTitle = "Add New Collection Object";
		SetWindowIcon( "rocket_launch" );

		Layout = Layout.Column();
		Layout.Margin = 4;
		Layout.Spacing = 4;

		container = new Widget( this );

		var properties = new PropertySheet( this );

		var nameLabel = new Label.Subtitle( "Add New Collection Object" );
		nameLabel.Margin = 16;

		var nameEdit = properties.AddLineEdit( "Collection Object Name", "" );
		var addbutton = new Button.Primary( "Add Collection", "add_circle" );
		addbutton.MaximumWidth = 100;
		addbutton.Clicked = () =>
		{
			gridmapToolDockWidget.CreateCollection( nameEdit.Text );
			/*
			var gameobject = new GameObject(true, nameEdit.Text );
			gameobject.Transform.Position = Vector3.Zero;
			gameobject.Tags.Add( "sprinkled" );
			gameobject.Tags.Add( "collection" );
			*/
			gridmapToolDockWidget.collectionDropDown.AddItem( nameEdit.Text );
			Close();
		};
		Layout.Add( nameLabel );
		Layout.Add( properties );
		Layout.Add( addbutton );
		Layout.Add( container );

		Show();
	}
	public string SetButtonIcon = "settings";

	public Button buttonIcon;
}
