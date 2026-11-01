using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Petrichor.Mobile.UI;

// A page of results is one adapter update, rather than dozens of layouts and
// empty-view transitions. Item property notifications still support selection.
public sealed class RowCollection : ObservableCollection<MediaRow>
{
    public void ReplaceWith(IEnumerable<MediaRow> rows)
    {
        var values=rows.ToArray();CheckReentrancy();Items.Clear();foreach(var value in values)Items.Add(value);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
    public void AddRange(IEnumerable<MediaRow> rows)
    {
        var values=rows.ToList();if(values.Count==0)return;CheckReentrancy();var start=Count;foreach(var value in values)Items.Add(value);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,values,start));
    }
}
