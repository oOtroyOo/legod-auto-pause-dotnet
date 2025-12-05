namespace LegodPause.Service.Base;

public abstract class Singleton<T> where T : class, new()
{
    private static readonly object _lock = new object();

    public static T Instance
    {
        get
        {
            if (null == field)
            {
                lock (_lock)
                {
                    if (field is null)
                    {
                        field = new T();
                    }
                }
            }

            return field;
        }
    }

    protected Singleton()
    {
    }
}