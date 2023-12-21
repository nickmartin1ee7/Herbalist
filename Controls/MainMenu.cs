using Godot;

public partial class MainMenu : Control
{
	private void _on_button_pressed()
	{
		GetTree().ChangeSceneToFile(Scenes.World);
	}
}
