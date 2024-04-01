using Sandbox;

public sealed class ItemChest : Interactable, Component.ITriggerListener
{
	List<PrefabFile> Items;

	[Property]
	[Group( "Item Chest" )]
	GameObject ItemSpawnLocation { get; set; }

	[Property]
	[Group( "Item Chest" )]
	ItemRarity Rarity { get; set; } = ItemRarity.Common;

	[Property]
	bool UseRandomItem { get; set; } = true;

	[Property]
	GameObject Top { get; set; }

	PrefabScene RandomItem { get; set; }

	protected override void OnStart()
	{
		base.OnStart();

		//Load the items from a folder.
		Items = ResourceLibrary.GetAll<PrefabFile>().Where( x => x.ResourcePath.StartsWith( GetPath( Rarity ) ) ).ToList();

		if ( UseRandomItem )
		{
			RandomItem = SceneUtility.GetPrefabScene( Items[Random.Shared.Int( 0, Items.Count - 1 )] );
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( IsOpen )
		{
			Top.Transform.Rotation = Rotation.Slerp( Top.Transform.Rotation, Rotation.From( new Angles( 0, 0, -120 ) + Transform.Rotation.Angles() ), Time.Delta * 5f );
			return;
		}
	}

	public override void OnInteract(GameObject player)
	{
		OpenChest(player);
	}

	public void OpenChest(GameObject player )
	{
		//Check if the player has enough coins
		var stats = player.Components.Get<PlayerStats>();

		if ( stats != null && stats.PlayerCoinsAndXp[PlayerStats.CoinsAndXp.Coins] >= Cost )
		{
			stats.AddCoin( -Cost );
			SpawnItem();

			IsOpen = true;
			//DestroyUI();
		}
		else
		{
			Log.Info( stats.PlayerCoinsAndXp[PlayerStats.CoinsAndXp.Coins] );
		}
	}

	void SpawnItem()
	{
		//var randomItem = SceneUtility.GetPrefabScene( Items[Random.Shared.Int( 0, Items.Count - 1 )] );

		var item = RandomItem.Clone();
		if ( ItemSpawnLocation != null )
			item.Transform.Position = ItemSpawnLocation.Transform.Position;

		var rb = item.Components.Get<Rigidbody>( FindMode.EnabledInSelfAndChildren );
		if ( rb != null )
		{
			rb.ApplyForce( Vector3.Up * 5000000f * 10 );
			rb.ApplyForce( Vector3.Left * Random.Shared.Float( -5000f, 5000f ) );
			rb.ApplyForce( Vector3.Forward * Random.Shared.Float( -5000f, 5000f ) );
		}
	}
	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		//Log.Info( "OnTriggerEnter" );

		if ( IsOpen ) return;
		/*
		if ( other.GameObject.Tags.Has( "player" ) )
		{
			PlayerInside = other.GameObject;

			var parent = other.GameObject.Parent;
			var ui = parent.Components.Get<ScreenPanel>( FindMode.EnabledInSelfAndDescendants );
			var itemUI = ui.Components.Get<ItemsUI>( FindMode.EnabledInSelfAndDescendants );

			var pickupui = new ItemPickUp( Cost );

			itemUI.Panel.AddChild( pickupui );
		}
		*/
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		//Log.Info( "OnTriggerExit" );
		/*
		if ( other.GameObject.Tags.Has( "player" ) )
		{
			PlayerInside = null;

			var parent = other.GameObject.Parent;
			var ui = parent.Components.Get<ScreenPanel>( FindMode.EnabledInSelfAndDescendants );
			var itemUI = ui.Components.Get<ItemsUI>( FindMode.EnabledInSelfAndDescendants );

			itemUI.Panel.DeleteChildren();
		}
		*/
	}

	string GetPath( ItemRarity rarity )
	{
		switch ( rarity )
		{
			case ItemRarity.Common:
				return "prefab/items/common/";
			case ItemRarity.Uncommon:
				return "prefab/items/uncommon/";
			case ItemRarity.Rare:
				return "prefab/items/rare/";
			case ItemRarity.Epic:
				return "prefab/items/epic/";
			case ItemRarity.Legendary:
				return "prefab/items/legendary/";
			default:
				return "prefab/items/common/";
		}
	}

	enum ItemRarity
	{
		Common,
		Uncommon,
		Rare,
		Epic,
		Legendary
	}
}


