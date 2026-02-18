using System.Collections.ObjectModel;

namespace SeResResaver.Extensions
{
    /// <summary>
    /// Extensions for ObservableCollection
    /// </summary>
    public static class ObservableCollectionExtensions
    {
        /// <summary>
        /// Remove given items from the collection.
        /// </summary>
        /// <typeparam name="T">Element type.</typeparam>
        /// <param name="collection">Collection.</param>
        /// <param name="itemsToRemove">Items to remove from the collection.</param>
        public static void RemoveMany<T>(this ObservableCollection<T> collection,
                                IEnumerable<T> itemsToRemove)
        {
            var toRemove = new HashSet<T>(itemsToRemove);

            for (int i = collection.Count - 1; i >= 0; i--)
            {
                if (toRemove.Contains(collection[i]))
                {
                    collection.RemoveAt(i);
                }
            }
        }
    }
}
