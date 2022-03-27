﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using UniRx;
using UnityEngine.Assertions;

namespace VitroStake.RxPort {
  using Internal;

  public abstract class StreamPort {
    protected static class Stream {
      public static void OnNext<TNotice>(TNotice notice) where TNotice : Enum {
        var observer = SubjectStore<TNotice, Unit>.GetOrCreateObserver(notice);
        observer.OnNext(Unit.Default);
      }

      public static IObservable<Unit> Of<TNotice>(TNotice notice) where TNotice : Enum {
        var observable = SubjectStore<TNotice, Unit>.GetOrCreateObservable(notice);
        Assert.IsNotNull(StreamDisposer.Instance);
        return observable.TakeUntilDestroy(StreamDisposer.Instance);
      }
    }

    protected static class Stream<TPayload> {
      public static void OnNext<TNotice>(TNotice notice, TPayload payload) where TNotice : Enum {
        if (payload == null)
          throw new ArgumentNullException();

        var observer = SubjectStore<TNotice, TPayload>.GetOrCreateObserver(notice);
        observer.OnNext(payload);
      }

      public static IObservable<TPayload> Of<TNotice>(TNotice notice) where TNotice : Enum {
        var observable = SubjectStore<TNotice, TPayload>.GetOrCreateObservable(notice);
        Assert.IsNotNull(StreamDisposer.Instance);
        return observable.TakeUntilDestroy(StreamDisposer.Instance);
      }
    }

    public StreamPort Open() {
      Assert.IsFalse(_opening);
      Assert.IsTrue(IsValid);

      OpenCore();
      _opening = true;

      StreamPortStore.UpdateOrAddPort(this);

      return this;
    }

    public void Close() {
      _disposables.Clear();
      _opening = false;
    }

    protected void Register(params IDisposable[] streams) {
      foreach (var stream in streams)
        _disposables.Add(stream);
    }

    protected abstract void OpenCore();
    public abstract bool IsValid { get; }

    public bool IsOpen  => _opening;
    public bool IsClose => !_opening;

    public StreamPort() {
      _disposables = new CompositeDisposable();
    }

    // CompositeDisposable is not automatically disposed
    ~StreamPort() {
      _disposables.Dispose();
    }

    protected CompositeDisposable _disposables;
    private bool _opening;
  }

  public static class StreamPortStore {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void OnRuntimeInitialize() {
      _ports = new Dictionary<Type, StreamPort>();
    }

    public static bool HasPort(Type type) {
      return _ports.ContainsKey(type);
    }

    public static bool TryGetPort<TPort>(out TPort port) where TPort : StreamPort {
      return TryGetPortImpl(out port, p => true);
    }

    public static bool TryGetOpenPort<TPort>(out TPort port) where TPort : StreamPort {
      return TryGetPortImpl(out port, p => p.IsOpen);
    }

    public static bool TryGetClosePort<TPort>(out TPort port) where TPort : StreamPort {
      return TryGetPortImpl(out port, p => p.IsClose);
    }

    private static bool TryGetPortImpl<TPort>(out TPort port, Func<StreamPort, bool> condition) where TPort : StreamPort {
      var type = typeof(TPort);
      port = null;

      if (HasPort(type)) {
        var target = (TPort)_ports[type];

        if (target.IsValid && condition(target)) {
          port = target;
          return true;
        }
      }

      return false;
    }

    internal static void UpdateOrAddPort(StreamPort port) {
      _ports[port.GetType()] = port;
    }

    private static Dictionary<Type, StreamPort> _ports;
  }
}
