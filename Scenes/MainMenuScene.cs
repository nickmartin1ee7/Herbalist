using Godot;

public partial class MainMenuScene : Node2D
{
	private Button _startButton;
	private Button _restartButton;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		var buttonsNode = GetNode<HBoxContainer>("Buttons");
		var spacer = buttonsNode.GetNode<Container>("Spacer");

		_startButton = buttonsNode.GetNode<Button>("StartButton");
		_restartButton = buttonsNode.GetNode<Button>("RestartButton");

		_startButton.Pressed += StartGame;
		_restartButton.Pressed += RestartGame;

		if (DataStorage.HasPreviousSession)
		{
			_startButton.Text = "Resume";
		}
		else
		{
			_restartButton.Visible = spacer.Visible = false;
		}
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
		DataStorage.Delete();

		StartGame();
	}
}
