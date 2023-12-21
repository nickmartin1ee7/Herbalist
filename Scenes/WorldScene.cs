using Godot;
using System;

public partial class WorldScene : Node2D
{
	private VBoxContainer _vbox;

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

		_vbox.AddChild(section);
	}
}
