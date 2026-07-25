public sealed class OneTouchIntentBuffer
{
    public OneTouchIntent Intent { get; private set; }
    public bool IsPreparing => Intent != OneTouchIntent.None;

    public void Queue(OneTouchIntent intent)
    {
        Intent = intent;
    }

    public void Clear()
    {
        Intent = OneTouchIntent.None;
    }

    public OneTouchIntent Consume()
    {
        OneTouchIntent intent = Intent;
        Clear();
        return intent;
    }
}
