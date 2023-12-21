using Godot;
using System;

public partial class UpgradableSection : Node2D
{
	private Button _button;
	private ProgressBar _progressBar;
	private Label _label;

	public const string ResourcePath = "res://Controls/upgradable_section.tscn";

	// Called when the node enters the scene tree for the first time.

	public override void _Ready()
	{
		_button = GetNode<Button>("Button");
		_progressBar = GetNode<ProgressBar>("ProgressBar");
		_label = GetNode<Label>("Label");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void _on_button_pressed()
	{
		_ = int.TryParse(_button.Text, out var num);
		num++;
		_button.Text = $"{num}";
	}
}
