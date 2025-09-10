using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;


namespace GUI.Shared.Helpers
{
  public enum ETimerType
  {
    OneShot,
    Periodic
  }

  public class TimerObject
  {
    public Guid Id;
   // public DispatchTimer Timer;
    public System.Timers.Timer BackgroundTimer;
    public Action Callback;
    public float Time;
    public string Grouping;
    public ETimerType TimerType;
    public bool IsValid;
    public Object ParentControl;

    public void Restart()
    {
      TimerHelper.RestartAction(this);
    }

    public void Trigger()
    {
      TimerHelper.TriggerAction(Id);
    }

    public void TriggerGroup()
    {
      if (ParentControl != null)
        TimerHelper.TriggerActions(ParentControl);
      else
        TimerHelper.TriggerActions(Grouping);
    }

    public void Abort()
    {
      TimerHelper.AbortAction(Id);
    }

    public void AbortGroup()
    {
      if (ParentControl != null)
        TimerHelper.AbortActions(ParentControl);
      else
        TimerHelper.AbortActions(Grouping);
    }
  }

  public static class TimerHelper
  {
    private static readonly Dictionary<Guid, TimerObject> Timers = new Dictionary<Guid, TimerObject>();
    private static readonly List<CancellationTokenSource> ValueWaitCancelTokens = new List<CancellationTokenSource>();

    //public static void WaitForValue<T>(Object obj, Expression<Func<T>> expr, T value, Action callback)
    //{
    //  var tokenSource = new CancellationTokenSource();

    //  Task.Run(() =>
    //  {
    //    var val = default(T);

    //    bool cancelled = false;

    //    do
    //    {
    //      if (tokenSource.Token.IsCancellationRequested)
    //      {
    //        cancelled = true;
    //        break;
    //      }

    //      Thread.Sleep(100);

    //      obj.Dispatcher.Invoke(() =>
    //      {
    //        val = expr.Compile()();
    //      });

    //    } while (!val.Equals(value));

    //    if (!cancelled)
    //      callback?.Invoke();

    //  }, tokenSource.Token);

    //  ValueWaitCancelTokens.Add(tokenSource);
    //}

    public static void Finish()
    {
      foreach (var waitTask in ValueWaitCancelTokens)
      {
        waitTask.Cancel();
      }
    }

    public static bool TriggerAction(Guid id)
    {
      if (!Timers.TryGetValue(id, out var o)) return false;

      o.Callback?.Invoke();

      if (o.TimerType == ETimerType.OneShot)
      {
        StopTimer(o);
      }

      return true;
    }

    public static void TriggerActions(string grouping)
    {
      foreach (var timer in Timers.Where(o => o.Value.Grouping == grouping).ToArray())
      {
        TriggerAction(timer.Key);
      }
    }

    public static void TriggerActions(Object parentControl)
    {
      if (parentControl == null) return;

      foreach (var timer in Timers.Where(o => o.Value.ParentControl == parentControl).ToArray())
      {
        TriggerAction(timer.Key);
      }
    }

    public static bool AbortAction(Guid id)
    {
      if (!Timers.TryGetValue(id, out var o)) return false;

      //o.Timer?.Stop();

      o.BackgroundTimer?.Stop();
      o.IsValid = false;
      Timers.Remove(id);
      return true;
    }

    public static void AbortActions(string grouping)
    {
      foreach (var timer in Timers.Where(o => o.Value.Grouping == grouping).ToArray())
      {
        AbortAction(timer.Key);
      }
    }

    public static void AbortActions(Object parentControl)
    {
      if (parentControl == null) return;

      foreach (var timer in Timers.Where(o => o.Value.ParentControl == parentControl).ToArray())
      {
        AbortAction(timer.Key);
      }
    }

    public static TimerObject DoEvery(Action callback, float timeMS, bool useMainThread)
    {
      return DoEvery(callback, timeMS, "", null, useMainThread);
    }

    public static TimerObject DoEvery(Action callback, float timeMS, string grouping, bool useMainThread)
    {
      return DoEvery(callback, timeMS, grouping, null, useMainThread);
    }

    public static TimerObject DoEvery(Action callback, float timeMS, Object parentControl, bool useMainThread)
    {
      return DoEvery(callback, timeMS, "", parentControl, useMainThread);
    }

    private static TimerObject DoEvery(Action callback, float timeMS, string grouping, Object parentControl, bool useMainThread)
    {
      return CreateTimer(callback, timeMS, grouping, parentControl, ETimerType.Periodic, useMainThread);
    }

    public static TimerObject DoAfter(Action callback, float timeMS, bool useMainThread)
    {
      return DoAfter(callback, timeMS, "", null, useMainThread);
    }

    public static TimerObject DoAfter(Action callback, float timeMS, string grouping, bool useMainThread)
    {
      return DoAfter(callback, timeMS, grouping, null, useMainThread);
    }

    public static TimerObject DoAfter(Action callback, float timeMS, Object parentControl, bool useMainThread)
    {
      return DoAfter(callback, timeMS, "", parentControl, useMainThread);
    }

    private static TimerObject DoAfter(Action callback, float timeMS, string grouping, Object parentControl, bool useMainThread)
    {
      return CreateTimer(callback, timeMS, grouping, parentControl, ETimerType.OneShot, useMainThread);
    }

    public delegate void MyEventHandler(TimerObjectEventArgs ea);

    //public event MyEventHandler SomethingHappened;

    public class TimerObjectEventArgs : EventArgs
    {
      public bool Cancel;
    }


    private static TimerObject CreateTimer(Action callback, float timeMS, string grouping, Object parentControl, ETimerType type, bool useMainThread)
    {
      var guid = Guid.NewGuid();
      var timerObject = new TimerObject();

      void TimerTick()
      {
        callback?.Invoke();

        if (timerObject.TimerType == ETimerType.OneShot)
        {
          StopTimer(timerObject);
        }
      }

      timeMS = timeMS <= 0 ? 1 : timeMS;

      //if (useMainThread)
      //{
      //  var timer = new Timer();
      //  timer.Interval = TimeSpan.FromMilliseconds(timeMS);

      //  timer.Tick += (sender, e) =>
      //  {
      //    TimerTick();
      //  };

      //  timer.Start();

      //  timerObject.Timer = timer;
      //}
      //else
      {
        var timer = new System.Timers.Timer();

        timer.Interval = timeMS;
        timer.Elapsed += (sender, args) => TimerTick();
        timer.Enabled = true;
        timer.AutoReset = type == ETimerType.Periodic;
        timer.Start();

        timerObject.BackgroundTimer = timer;
      }

      timerObject.Time = timeMS;
      timerObject.Callback = callback;
      timerObject.Grouping = grouping;
      timerObject.Id = guid;
      timerObject.TimerType = type;
      timerObject.IsValid = true;
      timerObject.ParentControl = parentControl;

      Timers.Add(guid, timerObject);

      return timerObject;
    }

    private static void StopTimer(TimerObject timer)
    {
      timer.IsValid = false;

     // timer.Timer?.Stop();
      timer.BackgroundTimer?.Stop();

      Timers.Remove(timer.Id);
    }

    public static void RestartAction(TimerObject timerObject)
    {
      StopTimer(timerObject);

      timerObject.IsValid = true;

      //timerObject.Timer?.Start();
      timerObject.BackgroundTimer?.Start();

      if (!Timers.ContainsKey(timerObject.Id))
        Timers.Add(timerObject.Id, timerObject);
    }
  }
}