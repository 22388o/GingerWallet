using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.SearchBar.Patterns;
using WalletWasabi.Fluent.ViewModels.SearchBar.SearchItems;

namespace WalletWasabi.Fluent.ViewModels.SearchBar;

public class SearchBarViewModel : ReactiveObject
{
	private readonly ReadOnlyObservableCollection<SearchItemGroup> _groups;
	private bool _isSearchListVisible;
	private string _searchText;

	public SearchBarViewModel(IObservable<IChangeSet<ISearchItem, ComposedKey>> itemsObservable)
	{
		var filterPredicate = this
			.WhenAnyValue(x => x.SearchText)
			.Throttle(TimeSpan.FromMilliseconds(250), RxApp.TaskpoolScheduler)
			.DistinctUntilChanged()
			.Select(SearchItemFilterFunc);

		itemsObservable
			.RefCount()
			.Filter(filterPredicate)
			.Transform(item => item is ActionableItem i ? new AutocloseActionableItem(i, () => IsSearchListVisible = false) : item)
			.Group(s => s.Category)
			.Transform(group => new SearchItemGroup(group.Key, group.Cache))
			.Bind(out _groups)
			.DisposeMany()
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe();

		ShowListCommand = ReactiveCommand.Create(() => IsSearchListVisible = true);
	}

	public ReactiveCommand<Unit, bool> ShowListCommand { get; set; }

	public bool IsSearchListVisible
	{
		get => _isSearchListVisible;
		set => this.RaiseAndSetIfChanged(ref _isSearchListVisible, value);
	}

	public ReadOnlyObservableCollection<SearchItemGroup> Groups => _groups;

	public string SearchText
	{
		get => _searchText;
		set => this.RaiseAndSetIfChanged(ref _searchText, value);
	}

	private static Func<ISearchItem, bool> SearchItemFilterFunc(string? text)
	{
		return searchItem =>
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return searchItem.IsDefault;
			}

			var containsName = searchItem.Name.Contains(text, StringComparison.InvariantCultureIgnoreCase);
			var containsAnyTag =
				searchItem.Keywords.Any(s => s.Contains(text, StringComparison.InvariantCultureIgnoreCase));
			return containsName || containsAnyTag;
		};
	}
}