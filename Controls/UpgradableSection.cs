using Godot;
using System;

public partial class UpgradableSection : Node2D
{
	private Button _button;
	private ProgressBar _progressBar;
	private Label _label;
	private Seed _seed;

	public Seed SeedType
	{
		get => _seed;

		set
		{
			if (_seed == value)
			{
				return;
			}

			_seed = value;
			NotifyStateChanged();
		}
	}
	public decimal Multiplier { get; private set; }
	public decimal Rate => (decimal)SeedType * Multiplier;

	public const string ResourcePath = "res://Controls/upgradable_section.tscn";

	// Called when the node enters the scene tree for the first time.

	public override void _Ready()
	{
		_button = GetNode<Button>("Button");
		_progressBar = GetNode<ProgressBar>("ProgressBar");
		_label = GetNode<Label>("Label");

		NotifyStateChanged();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void UpgradeMultiplier()
	{
		Multiplier++;
		NotifyStateChanged();
	}

	private void _on_button_pressed()
	{
		UpgradeMultiplier();
	}

	private void NotifyStateChanged()
	{
		if (_label is not null)
		{
			_label.Text = SeedType.ToString();
		}

		if (_button is not null)
		{
			_button.Text = $"x{Multiplier}";
		}
	}
}
