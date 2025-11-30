using System.Collections.Generic;
using UnityEngine;

public static class LocalizationRegistry
{
    static readonly HashSet<ILocalizable> _items = new HashSet<ILocalizable>();

    public static void Register(ILocalizable item)
    {
        if (item == null) return;
        _items.Add(item);
    }

    public static void Unregister(ILocalizable item)
    {
        if (item == null) return;
        _items.Remove(item);
    }

    public static void RefreshAll()
    {
        var toRemove = new List<ILocalizable>();
        foreach (ILocalizable it in _items)
        {
            if (it is Object o && o == null)
            {
                toRemove.Add(it);
                continue;
            }
            it?.RefreshLocale();
        }
        foreach (ILocalizable dead in toRemove) _items.Remove(dead);
    }

    public static void RescanSceneAndRegisterAll()
    {
        var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        foreach (var mb in all)
        {
            if (mb is ILocalizable loc)
                _items.Add(loc);
        }
    }
}