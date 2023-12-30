using Godot;

public partial class SwipeableControl : Control
{
    private enum SnapState
    {
        None = 0,
        SnappingToTop,
        SnappingToBottom,
    }


    private float _maxY;
    private bool _dragging;
    private SnapState _snapState;
    private Vector2 _initialPos;


    public override void _Ready()
    {
        _initialPos = Position;
        _maxY = 0;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent
            && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            if (mouseEvent.Pressed)
            {
                _dragging = GetRect().HasPoint(mouseEvent.Position);
            }
            else if (_dragging)
            {
                // Snap to top
                if (_snapState == SnapState.SnappingToTop)
                {
                    GD.Print($"Snaping to top... Position: {Position.Y} -> {_maxY}");
                    Position = new Vector2(
                        Position.X,
                        _maxY);
                }
                // Snap to bottom
                else if (_snapState == SnapState.SnappingToBottom)
                {
                    GD.Print($"Snaping to bottom... Position: {Position.Y} -> {_initialPos.Y}");
                    Position = new Vector2(
                        Position.X,
                        _initialPos.Y);
                }

                _snapState = SnapState.None;
                _dragging = false;
            }
        }

        if (@event is InputEventMouseMotion mouseMotionEvent
            && _dragging)
        {
            const float DeadZone = 5.0f;

            var yChange = Mathf.Round(mouseMotionEvent.Relative.Y);
            var newYPos = Position.Y + yChange;
            var lowEnough = newYPos > _maxY;
            var highEnough = newYPos < _initialPos.Y;

            // Deadzone filtering
            if (Mathf.Abs(yChange) < DeadZone)
            {
                return;
            }

            _snapState = yChange > 0 // Going down
                ? SnapState.SnappingToBottom
                : SnapState.None;

            _snapState = yChange < -0 // Going up
                ? SnapState.SnappingToTop
                : _snapState;

            // Move if within bounds
            if (lowEnough && highEnough)
            {
                GD.Print($"Moving: lowEnough = {lowEnough}. highEnough = {highEnough}. Position: {Position.Y} + {yChange} = {newYPos}. Snap State: {_snapState} _initialPos.Y = {_initialPos.Y}. MaxY = {_maxY}.");

                Position = new Vector2(
                    Position.X,
                    newYPos);
            }
            else
            {
                GD.Print($"Cannot move: lowEnough = {lowEnough}. highEnough = {highEnough}. Position = {Position.Y} + {yChange} => {newYPos}. Snap State = {_snapState} _initialPos.Y = {_initialPos.Y}. MaxY = {_maxY}.");
            }
        }
    }
}
