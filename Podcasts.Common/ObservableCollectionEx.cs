using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Podcasts
{
    public sealed class ObservableCollectionEx<T> : ObservableCollection<T>
    {
        public ObservableCollectionEx()
        {
        }

        public ObservableCollectionEx(List<T> list) : base(list)
        {
        }

        public ObservableCollectionEx(IEnumerable<T> collection) : base(collection)
        {
        }

        /// <summary> 
        /// Adds the elements of the specified collection to the end of the ObservableCollection(Of T). 
        /// </summary> 
        public void AddRange(
            IEnumerable<T> itemsToAdd)
        {
            if (itemsToAdd == null)
            {
                throw new ArgumentNullException("itemsToAdd");
            }

            CheckReentrancy();

            int startIndex = Count;
            var changedItems = itemsToAdd is List<T> ? (List<T>)itemsToAdd : new List<T>(itemsToAdd);
            foreach (var i in changedItems)
            {
                Items.Add(i);
            }

            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, changedItems, startIndex));
        }
    }
}
