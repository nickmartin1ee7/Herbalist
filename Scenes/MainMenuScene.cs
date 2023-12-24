using Godot;

public partial class MainMenuScene : Node2D
{
	private Button _startButton;
	private Button _restartButton;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_startButton = GetNode<HBoxContainer>("Buttons").GetNode<Button>("StartButton");
		_restartButton = GetNode<HBoxContainer>("Buttons").GetNode<Button>("RestartButton");

		_startButton.Pressed += StartGame;
		_restartButton.Pressed += RestartGame;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void StartGame()
	{
		GetTree().ChangeSceneToFile(Scenes.World);
	}

	private void RestartGame()
	{
		// TODO, delete save

		StartGame();
	}
}
