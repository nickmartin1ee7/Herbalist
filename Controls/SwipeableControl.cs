using Godot;

public partial class SwipeableControl : Control
{
	private bool _dragging;

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Left)
			{
				if (mouseEvent.Pressed)
				{
					_dragging = GetRect().HasPoint(mouseEvent.Position);
				}
				else
				{
					_dragging = false;
				}
			}
		}

		if (@event is InputEventMouseMotion mouseMotionEvent)
		{
			if (_dragging)
			{
				Position += mouseMotionEvent.Relative;
			}
		}
	}
}
