using Godot;
using System;
using System.Collections.Generic;

public partial class WorldScene : Node2D
{
	private VBoxContainer _vbox;
	private Dictionary<Seed, UpgradableSection> _sections = new();
	private int _lastSeedIndex;
	private readonly Seed[] _seeds = Enum.GetValues<Seed>();

	private Seed? RequestNextSeed()
	{
		var nextSeedIndex = _lastSeedIndex + 1;

		if (nextSeedIndex >= _seeds.Length)
			return null;

		var nextSeed = _seeds[nextSeedIndex];
		_lastSeedIndex = nextSeedIndex;

		return nextSeed;
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_vbox = GetNode<ScrollContainer>("ScrollContainer")
			.GetNode<VBoxContainer>("VBoxContainer");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void _on_buy_button_pressed()
	{
		AddNewUpgradableSection();
		GD.Print($"Added upgradable section to {_vbox}. Child nodes: {_vbox?.GetChildCount()}");
	}

	private void AddNewUpgradableSection()
	{
		const float sectionHeight = 120; // Height of each Section
		const float spacing = 80; // Vertical spacing between Sections

		// Load the Section scene and instance it
		var upgradableSectionScene = GD.Load<PackedScene>(UpgradableSection.ResourcePath);
		var section = upgradableSectionScene.Instantiate<UpgradableSection>();

		// Set the Section's position based on its index
		var newPos = new Vector2(section.Position.X, _vbox.GetChildCount() * (sectionHeight + spacing));
		section.Position = newPos;

		section.Scale = new Vector2(2.5f, 2.5f);

		var preparedSection = PrepareNextSection(section);

		if (preparedSection is not null)
		{
			_vbox.AddChild(preparedSection);
		}
	}

	private UpgradableSection? PrepareNextSection(UpgradableSection section)
	{
		var nextSeed = RequestNextSeed();

		if (!nextSeed.HasValue)
		{

			return null;
		}

		section.SeedType = nextSeed.Value;
		_sections.Add(nextSeed.Value, section);

		return section;
	}

}
