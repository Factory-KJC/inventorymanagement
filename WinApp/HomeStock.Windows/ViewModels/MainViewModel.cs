using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Windows.Input;
using HomeStock.Windows.Infrastructure;
using HomeStock.Windows.Models;
using HomeStock.Windows.Services;

namespace HomeStock.Windows.ViewModels;

/// <summary>Windowsクライアントの画面状態と全ユースケースを管理します。</summary>
public sealed class MainViewModel : ObservableObject
{
    private static readonly string[] SectionNames = ["ホーム", "商品一覧", "保管場所一覧", "在庫一覧・棚卸", "買い物リスト", "商品・保管場所登録", "スキャン履歴"];
    private readonly HomeStockApiClient _apiClient;
    private readonly ContinuousReceivingService _receivingService;
    private readonly BarcodeInputBuffer _barcodeInputBuffer;
    private readonly IUserInteraction _userInteraction;
    private readonly IInventoryExportService _exportService;
    private IReadOnlyList<ProductResponse> _products = [];
    private IReadOnlyList<InventoryLotResponse> _inventory = [];
    private bool _isInitialized;
    private bool _isAuthenticated;
    private int _selectedSectionIndex;
    private string _apiUrl = "http://localhost:8080/";
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _setupToken = string.Empty;
    private string _status = "未接続";
    private string _simulatedBarcode = string.Empty;
    private string _productSearch = string.Empty;
    private string _inventoryFilter = string.Empty;
    private string _productName = string.Empty;
    private string _productBarcode = string.Empty;
    private string _productUnit = "個";
    private string _reorderPoint = string.Empty;
    private string _targetQuantity = string.Empty;
    private string _locationName = string.Empty;
    private string _locationSortOrder = "0";
    private string _stockQuantity = "1";
    private DateTime? _stockExpiryDate;
    private bool _hasStockExpiry;
    private string _countedQuantity = string.Empty;
    private string _shoppingName = string.Empty;
    private string _shoppingQuantity = "1";
    private int _selectedPrintPaperWidthIndex = 1;
    private string _printPreview = "プレビューを表示すると、ここに印刷内容が表示されます。";
    private ProductResponse? _selectedProduct;
    private LocationResponse? _selectedLocation;
    private LocationResponse? _selectedScanLocation;
    private ProductResponse? _selectedStockProduct;
    private LocationResponse? _selectedStockLocation;
    private InventoryLotResponse? _selectedInventory;
    private ShoppingItemResponse? _selectedShoppingItem;
    private Guid? _editingProductId;
    private Guid? _editingLocationId;
    private int _productCount;
    private int _lowStockCount;
    private int _expiringCount;
    private int _shoppingCount;

    public MainViewModel(
        HomeStockApiClient apiClient,
        ContinuousReceivingService receivingService,
        BarcodeInputBuffer barcodeInputBuffer,
        IUserInteraction userInteraction,
        IInventoryExportService exportService)
    {
        _apiClient = apiClient;
        _receivingService = receivingService;
        _barcodeInputBuffer = barcodeInputBuffer;
        _userInteraction = userInteraction;
        _exportService = exportService;

        LoginCommand = new AsyncRelayCommand(LoginAsync);
        RegisterCommand = new AsyncRelayCommand(RegisterAsync);
        LogoutCommand = new RelayCommand(Logout);
        SimulateScanCommand = new AsyncRelayCommand(() => ProcessBarcodeAsync(SimulatedBarcode));
        RefreshCommand = new AsyncRelayCommand(() => RunUiActionAsync(LoadReferenceDataAsync));
        SaveProductCommand = new AsyncRelayCommand(SaveProductAsync);
        EditProductCommand = new RelayCommand(EditProduct);
        CancelProductEditCommand = new RelayCommand(ClearProductEditor);
        DeleteProductCommand = new AsyncRelayCommand(DeleteProductAsync);
        SaveLocationCommand = new AsyncRelayCommand(SaveLocationAsync);
        EditLocationCommand = new RelayCommand(EditLocation);
        CancelLocationEditCommand = new RelayCommand(ClearLocationEditor);
        DeleteLocationCommand = new AsyncRelayCommand(DeleteLocationAsync);
        ReceiveCommand = new AsyncRelayCommand(ReceiveAsync);
        ConsumeCommand = new AsyncRelayCommand(ConsumeAsync);
        AdjustCommand = new AsyncRelayCommand(AdjustAsync);
        ExportCommand = new AsyncRelayCommand(ExportAsync);
        AddShoppingItemCommand = new AsyncRelayCommand(AddShoppingItemAsync);
        GenerateShoppingCommand = new AsyncRelayCommand(GenerateShoppingAsync);
        ToggleShoppingItemCommand = new AsyncRelayCommand(ToggleShoppingItemAsync);
        PreviewShoppingListCommand = new AsyncRelayCommand(PreviewShoppingListAsync);
        PrintShoppingListCommand = new AsyncRelayCommand(PrintShoppingListAsync);
    }

    public IReadOnlyList<string> Sections => SectionNames;
    public ObservableCollection<ProductResponse> VisibleProducts { get; } = [];
    public ObservableCollection<LocationResponse> Locations { get; } = [];
    public ObservableCollection<InventoryLotResponse> VisibleInventory { get; } = [];
    public ObservableCollection<InventoryLotResponse> ExpiringInventory { get; } = [];
    public ObservableCollection<ShoppingItemResponse> ShoppingItems { get; } = [];
    public ObservableCollection<string> ScanHistory { get; } = [];
    public IReadOnlyList<string> PrintPaperWidths { get; } = ["58 mm", "80 mm"];

    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand SimulateScanCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SaveProductCommand { get; }
    public ICommand EditProductCommand { get; }
    public ICommand CancelProductEditCommand { get; }
    public ICommand DeleteProductCommand { get; }
    public ICommand SaveLocationCommand { get; }
    public ICommand EditLocationCommand { get; }
    public ICommand CancelLocationEditCommand { get; }
    public ICommand DeleteLocationCommand { get; }
    public ICommand ReceiveCommand { get; }
    public ICommand ConsumeCommand { get; }
    public ICommand AdjustCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand AddShoppingItemCommand { get; }
    public ICommand GenerateShoppingCommand { get; }
    public ICommand ToggleShoppingItemCommand { get; }
    public ICommand PreviewShoppingListCommand { get; }
    public ICommand PrintShoppingListCommand { get; }

    public string ApiUrl { get => _apiUrl; set => SetProperty(ref _apiUrl, value); }
    public string Username { get => _username; set => SetProperty(ref _username, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string SetupToken { get => _setupToken; set => SetProperty(ref _setupToken, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public bool IsAuthenticated { get => _isAuthenticated; private set => SetProperty(ref _isAuthenticated, value); }
    public string SimulatedBarcode { get => _simulatedBarcode; set => SetProperty(ref _simulatedBarcode, value); }
    public string ProductSearch { get => _productSearch; set { if (SetProperty(ref _productSearch, value)) ApplyProductFilter(); } }
    public string InventoryFilter { get => _inventoryFilter; set { if (SetProperty(ref _inventoryFilter, value)) ApplyInventoryFilter(); } }
    public string ProductName { get => _productName; set => SetProperty(ref _productName, value); }
    public string ProductBarcode { get => _productBarcode; set => SetProperty(ref _productBarcode, value); }
    public string ProductUnit { get => _productUnit; set => SetProperty(ref _productUnit, value); }
    public string ReorderPoint { get => _reorderPoint; set => SetProperty(ref _reorderPoint, value); }
    public string TargetQuantity { get => _targetQuantity; set => SetProperty(ref _targetQuantity, value); }
    public string LocationName { get => _locationName; set => SetProperty(ref _locationName, value); }
    public string LocationSortOrder { get => _locationSortOrder; set => SetProperty(ref _locationSortOrder, value); }
    public string StockQuantity { get => _stockQuantity; set => SetProperty(ref _stockQuantity, value); }
    public DateTime? StockExpiryDate { get => _stockExpiryDate; set => SetProperty(ref _stockExpiryDate, value); }
    public bool HasStockExpiry { get => _hasStockExpiry; set => SetProperty(ref _hasStockExpiry, value); }
    public string CountedQuantity { get => _countedQuantity; set => SetProperty(ref _countedQuantity, value); }
    public string ShoppingName { get => _shoppingName; set => SetProperty(ref _shoppingName, value); }
    public string ShoppingQuantity { get => _shoppingQuantity; set => SetProperty(ref _shoppingQuantity, value); }
    public int SelectedPrintPaperWidthIndex
    {
        get => _selectedPrintPaperWidthIndex;
        set
        {
            if (SetProperty(ref _selectedPrintPaperWidthIndex, value))
            {
                PrintPreview = "用紙幅を変更しました。プレビューを更新してください。";
            }
        }
    }
    public string PrintPreview { get => _printPreview; private set => SetProperty(ref _printPreview, value); }
    public ProductResponse? SelectedProduct { get => _selectedProduct; set => SetProperty(ref _selectedProduct, value); }
    public LocationResponse? SelectedLocation { get => _selectedLocation; set => SetProperty(ref _selectedLocation, value); }
    public LocationResponse? SelectedScanLocation { get => _selectedScanLocation; set => SetProperty(ref _selectedScanLocation, value); }
    public ProductResponse? SelectedStockProduct { get => _selectedStockProduct; set => SetProperty(ref _selectedStockProduct, value); }
    public LocationResponse? SelectedStockLocation { get => _selectedStockLocation; set => SetProperty(ref _selectedStockLocation, value); }
    public InventoryLotResponse? SelectedInventory { get => _selectedInventory; set => SetProperty(ref _selectedInventory, value); }
    public ShoppingItemResponse? SelectedShoppingItem { get => _selectedShoppingItem; set => SetProperty(ref _selectedShoppingItem, value); }
    public int ProductCount { get => _productCount; private set => SetProperty(ref _productCount, value); }
    public int LowStockCount { get => _lowStockCount; private set => SetProperty(ref _lowStockCount, value); }
    public int ExpiringCount { get => _expiringCount; private set => SetProperty(ref _expiringCount, value); }
    public int ShoppingCount { get => _shoppingCount; private set => SetProperty(ref _shoppingCount, value); }
    public string SaveProductText => _editingProductId.HasValue ? "商品を更新" : "商品を登録";
    public string SaveLocationText => _editingLocationId.HasValue ? "保管場所を更新" : "保管場所を登録";

    public int SelectedSectionIndex
    {
        get => _selectedSectionIndex;
        set
        {
            if (SetProperty(ref _selectedSectionIndex, value))
            {
                for (var i = 0; i < SectionNames.Length; i++)
                {
                    OnPropertyChanged($"IsSection{i}Visible");
                }
            }
        }
    }

    public bool IsSection0Visible => SelectedSectionIndex == 0;
    public bool IsSection1Visible => SelectedSectionIndex == 1;
    public bool IsSection2Visible => SelectedSectionIndex == 2;
    public bool IsSection3Visible => SelectedSectionIndex == 3;
    public bool IsSection4Visible => SelectedSectionIndex == 4;
    public bool IsSection5Visible => SelectedSectionIndex == 5;
    public bool IsSection6Visible => SelectedSectionIndex == 6;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await RunUiActionAsync(async cancellationToken =>
        {
            if (!await _apiClient.RestoreAsync(cancellationToken))
            {
                return;
            }

            IsAuthenticated = true;
            await LoadReferenceDataAsync(cancellationToken);
            Status = "保存済みの更新トークンで接続しました。";
        });
    }

    public async Task PushScannerCharacterAsync(char character)
    {
        var barcode = _barcodeInputBuffer.Push(character);
        if (barcode is not null && IsAuthenticated)
        {
            await ProcessBarcodeAsync(barcode);
        }
    }

    private async Task LoginAsync() => await RunUiActionAsync(async cancellationToken =>
    {
        await _apiClient.LoginAsync(GetApiUri(), Username.Trim(), Password, cancellationToken);
        Password = string.Empty;
        IsAuthenticated = true;
        await LoadReferenceDataAsync(cancellationToken);
        Status = "ログインしました。";
    });

    private async Task RegisterAsync() => await RunUiActionAsync(async cancellationToken =>
    {
        var message = await _apiClient.RegisterInitialUserAsync(GetApiUri(), Username.Trim(), Password, SetupToken, cancellationToken);
        Status = message;
    });

    private void Logout()
    {
        _apiClient.Logout();
        IsAuthenticated = false;
        Password = string.Empty;
        ClearLoadedData();
        Status = "ログアウトしました。";
    }

    private async Task ProcessBarcodeAsync(string barcode) => await RunUiActionAsync(async cancellationToken =>
    {
        if (SelectedScanLocation is null)
        {
            throw new InvalidOperationException("連続スキャンの保管場所を選択してください。");
        }

        var result = await _receivingService.ReceiveBarcodeAsync(barcode.Trim(), SelectedScanLocation.Id, cancellationToken);
        ScanHistory.Insert(0, $"{DateTime.Now:T} {result.Barcode} {result.Message}");
        Status = result.Message;
        SimulatedBarcode = string.Empty;
        if (!result.IsSuccess)
        {
            ProductBarcode = barcode.Trim();
            SelectedSectionIndex = 5;
            return;
        }

        await RefreshOperationalDataAsync(cancellationToken);
    });

    private async Task SaveProductAsync() => await RunUiActionAsync(async cancellationToken =>
    {
        var name = ProductName.Trim();
        var unit = ProductUnit.Trim();
        if (name.Length == 0 || unit.Length == 0)
        {
            throw new InvalidOperationException("商品名と単位を入力してください。");
        }

        var request = new UpdateProductRequest(name, EmptyToNull(ProductBarcode), unit, ParseOptionalDecimal(ReorderPoint, "補充点"), ParseOptionalDecimal(TargetQuantity, "目標在庫"));
        ProductResponse product;
        string status;
        if (_editingProductId.HasValue)
        {
            product = await _apiClient.UpdateProductAsync(_editingProductId.Value, request, cancellationToken);
            status = $"{product.Name} を更新しました。";
        }
        else
        {
            var suggestion = (await _apiClient.GetDeletedProductSuggestionsAsync(name, cancellationToken)).FirstOrDefault();
            if (suggestion is not null && await ConfirmRestoreAsync("商品", name, suggestion.Name))
            {
                product = await _apiClient.RestoreProductAsync(suggestion.Id, request, cancellationToken);
                status = $"削除済み商品 {suggestion.Name} を {product.Name} として復元しました。";
            }
            else
            {
                if (suggestion is not null && string.Equals(suggestion.Name, name, StringComparison.CurrentCultureIgnoreCase))
                {
                    throw new InvalidOperationException("同名の削除済み商品があります。復元するか、別の名称を入力してください。");
                }

                product = await _apiClient.CreateProductAsync(new CreateProductRequest(request.Name, request.Barcode, request.Unit, request.ReorderPoint, request.TargetQuantity), cancellationToken);
                status = $"{product.Name} を登録しました。";
            }
        }

        ClearProductEditor();
        await LoadProductsAsync(cancellationToken);
        await LoadDashboardAsync(cancellationToken);
        SelectedProduct = VisibleProducts.FirstOrDefault(item => item.Id == product.Id);
        Status = status;
    });

    private void EditProduct()
    {
        if (SelectedProduct is null)
        {
            Status = "編集する商品を選択してください。";
            return;
        }

        _editingProductId = SelectedProduct.Id;
        ProductName = SelectedProduct.Name;
        ProductBarcode = SelectedProduct.Barcode ?? string.Empty;
        ProductUnit = SelectedProduct.Unit;
        ReorderPoint = FormatDecimal(SelectedProduct.ReorderPoint);
        TargetQuantity = FormatDecimal(SelectedProduct.TargetQuantity);
        OnPropertyChanged(nameof(SaveProductText));
        SelectedSectionIndex = 5;
        Status = $"{SelectedProduct.Name} を編集中です。";
    }

    private async Task DeleteProductAsync()
    {
        if (SelectedProduct is null)
        {
            Status = "削除する商品を選択してください。";
            return;
        }

        var product = SelectedProduct;
        if (!await _userInteraction.ConfirmAsync("商品の削除確認", $"商品「{product.Name}」を削除しますか？\n在庫と履歴は保持されます。", "削除", "キャンセル"))
        {
            return;
        }

        await RunUiActionAsync(async cancellationToken =>
        {
            await _apiClient.DeleteProductAsync(product.Id, cancellationToken);
            if (_editingProductId == product.Id) ClearProductEditor();
            await LoadProductsAsync(cancellationToken);
            await LoadDashboardAsync(cancellationToken);
            Status = $"商品「{product.Name}」を削除しました。";
        });
    }

    private async Task SaveLocationAsync() => await RunUiActionAsync(async cancellationToken =>
    {
        var name = LocationName.Trim();
        if (name.Length == 0 || !int.TryParse(LocationSortOrder, out var sortOrder))
        {
            throw new InvalidOperationException("保管場所名と整数の表示順を入力してください。");
        }

        var request = new UpdateLocationRequest(name, sortOrder);
        LocationResponse location;
        string status;
        if (_editingLocationId.HasValue)
        {
            location = await _apiClient.UpdateLocationAsync(_editingLocationId.Value, request, cancellationToken);
            status = $"保管場所「{location.Name}」を更新しました。";
        }
        else
        {
            var suggestion = (await _apiClient.GetDeletedLocationSuggestionsAsync(name, cancellationToken)).FirstOrDefault();
            if (suggestion is not null && await ConfirmRestoreAsync("保管場所", name, suggestion.Name))
            {
                location = await _apiClient.RestoreLocationAsync(suggestion.Id, request, cancellationToken);
                status = $"削除済み保管場所「{suggestion.Name}」を {location.Name} として復元しました。";
            }
            else
            {
                if (suggestion is not null && string.Equals(suggestion.Name, name, StringComparison.CurrentCultureIgnoreCase))
                {
                    throw new InvalidOperationException("同名の削除済み保管場所があります。復元するか、別の名称を入力してください。");
                }

                location = await _apiClient.CreateLocationAsync(new CreateLocationRequest(name, sortOrder), cancellationToken);
                status = $"保管場所「{location.Name}」を登録しました。";
            }
        }

        ClearLocationEditor();
        await LoadLocationsAsync(location.Id, cancellationToken);
        await LoadInventoryAsync(cancellationToken);
        Status = status;
    });

    private void EditLocation()
    {
        if (SelectedLocation is null)
        {
            Status = "編集する保管場所を選択してください。";
            return;
        }

        _editingLocationId = SelectedLocation.Id;
        LocationName = SelectedLocation.Name;
        LocationSortOrder = SelectedLocation.SortOrder.ToString(CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(SaveLocationText));
        SelectedSectionIndex = 5;
        Status = $"保管場所「{SelectedLocation.Name}」を編集中です。";
    }

    private async Task DeleteLocationAsync()
    {
        if (SelectedLocation is null)
        {
            Status = "削除する保管場所を選択してください。";
            return;
        }

        var location = SelectedLocation;
        if (!await _userInteraction.ConfirmAsync("保管場所の削除確認", $"保管場所「{location.Name}」を削除しますか？\n在庫と履歴は保持されます。", "削除", "キャンセル"))
        {
            return;
        }

        await RunUiActionAsync(async cancellationToken =>
        {
            await _apiClient.DeleteLocationAsync(location.Id, cancellationToken);
            if (_editingLocationId == location.Id) ClearLocationEditor();
            await LoadLocationsAsync(null, cancellationToken);
            Status = $"保管場所「{location.Name}」を削除しました。";
        });
    }

    private async Task ReceiveAsync() => await RunUiActionAsync(async cancellationToken =>
    {
        var product = SelectedStockProduct ?? throw new InvalidOperationException("商品を選択してください。");
        var location = SelectedStockLocation ?? throw new InvalidOperationException("保管場所を選択してください。");
        var quantity = ParsePositiveDecimal(StockQuantity, "数量");
        DateOnly? expiresOn = HasStockExpiry && StockExpiryDate.HasValue ? DateOnly.FromDateTime(StockExpiryDate.Value) : null;
        await _apiClient.ReceiveAsync(new ReceiveStockRequest(product.Id, location.Id, quantity, expiresOn, "Windows入庫"), $"maui-receive-{Guid.NewGuid():N}", cancellationToken);
        await RefreshOperationalDataAsync(cancellationToken);
        Status = "入庫しました。";
    });

    private async Task ConsumeAsync() => await RunUiActionAsync(async cancellationToken =>
    {
        var product = SelectedStockProduct ?? throw new InvalidOperationException("商品を選択してください。");
        var quantity = ParsePositiveDecimal(StockQuantity, "数量");
        await _apiClient.ConsumeAsync(new ConsumeStockRequest(product.Id, quantity, SelectedStockLocation?.Id, "Windows消費"), $"maui-consume-{Guid.NewGuid():N}", cancellationToken);
        await RefreshOperationalDataAsync(cancellationToken);
        Status = "消費を記録しました。";
    });

    private async Task AdjustAsync() => await RunUiActionAsync(async cancellationToken =>
    {
        var lot = SelectedInventory ?? throw new InvalidOperationException("棚卸対象のロットを選択してください。");
        if (!decimal.TryParse(CountedQuantity, NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity) || quantity < 0)
        {
            throw new InvalidOperationException("棚卸実数には0以上の数値を入力してください。");
        }

        await _apiClient.AdjustAsync(new AdjustStockRequest(lot.LotId, quantity, "Windows棚卸"), $"maui-adjust-{Guid.NewGuid():N}", cancellationToken);
        await LoadInventoryAsync(cancellationToken);
        Status = $"{lot.ProductName} の在庫を {quantity} に調整しました。";
    });

    private async Task ExportAsync() => await RunUiActionAsync(async cancellationToken =>
    {
        if (await _exportService.ExportAsync(VisibleInventory, cancellationToken)) Status = "表示中の在庫をCSVへ出力しました。";
    });

    private async Task AddShoppingItemAsync() => await RunUiActionAsync(async cancellationToken =>
    {
        var name = ShoppingName.Trim();
        if (name.Length == 0) throw new InvalidOperationException("品名を入力してください。");
        var list = await _apiClient.AddShoppingItemAsync(new AddShoppingItemRequest(null, name, ParsePositiveDecimal(ShoppingQuantity, "数量")), cancellationToken);
        Replace(ShoppingItems, list.Items);
        ShoppingName = string.Empty;
        await LoadDashboardAsync(cancellationToken);
        Status = $"{name} を買い物リストへ追加しました。";
    });

    private async Task GenerateShoppingAsync() => await RunUiActionAsync(async cancellationToken =>
    {
        var list = await _apiClient.GenerateShoppingSuggestionsAsync(cancellationToken);
        Replace(ShoppingItems, list.Items);
        await LoadDashboardAsync(cancellationToken);
        Status = "在庫不足の商品を買い物リストへ反映しました。";
    });

    private async Task ToggleShoppingItemAsync() => await RunUiActionAsync(async cancellationToken =>
    {
        var item = SelectedShoppingItem ?? throw new InvalidOperationException("買い物項目を選択してください。");
        var status = item.Status == ShoppingItemStatus.Purchased ? ShoppingItemStatus.Pending : ShoppingItemStatus.Purchased;
        var list = await _apiClient.UpdateShoppingItemAsync(item.Id, new UpdateShoppingItemRequest(null, status), cancellationToken);
        Replace(ShoppingItems, list.Items);
        await LoadDashboardAsync(cancellationToken);
        Status = "購入状態を更新しました。";
    });

    private async Task PreviewShoppingListAsync() => await RunUiActionAsync(async cancellationToken =>
    {
        var paperWidth = SelectedPrintPaperWidthIndex == 0 ? PrintPaperWidth.Mm58 : PrintPaperWidth.Mm80;
        var preview = await _apiClient.GetPrintPreviewAsync(paperWidth, cancellationToken);
        PrintPreview = ReceiptPreviewFormatter.Format(preview.ReceiptLine, preview.PaperWidth);
        Status = $"{PrintPaperWidths[SelectedPrintPaperWidthIndex]}の印刷プレビューを更新しました。";
    });

    private async Task PrintShoppingListAsync() => await RunUiActionAsync(async cancellationToken =>
    {
        var paperWidth = SelectedPrintPaperWidthIndex == 0 ? PrintPaperWidth.Mm58 : PrintPaperWidth.Mm80;
        var preview = await _apiClient.GetPrintPreviewAsync(paperWidth, cancellationToken);
        PrintPreview = ReceiptPreviewFormatter.Format(preview.ReceiptLine, preview.PaperWidth);
        if (!await _userInteraction.ConfirmAsync("買い物リストの印刷", "表示中の内容を印刷しますか？", "印刷", "キャンセル"))
        {
            Status = "印刷をキャンセルしました。";
            return;
        }

        var job = await _apiClient.CreatePrintJobAsync(paperWidth, cancellationToken);
        Status = $"印刷ジョブを受け付けました（{job.Id}）。";
    });

    private async Task LoadReferenceDataAsync(CancellationToken cancellationToken)
    {
        await LoadProductsAsync(cancellationToken);
        await LoadLocationsAsync(null, cancellationToken);
        await LoadInventoryAsync(cancellationToken);
        await LoadShoppingAsync(cancellationToken);
        await LoadDashboardAsync(cancellationToken);
    }

    private async Task LoadProductsAsync(CancellationToken cancellationToken)
    {
        _products = await _apiClient.GetProductsAsync(null, null, cancellationToken);
        ApplyProductFilter();
        SelectedStockProduct ??= _products.FirstOrDefault();
    }

    private async Task LoadLocationsAsync(Guid? selectedId, CancellationToken cancellationToken)
    {
        var locations = await _apiClient.GetLocationsAsync(cancellationToken);
        Replace(Locations, locations);
        SelectedScanLocation = locations.FirstOrDefault(item => item.Id == selectedId) ?? locations.FirstOrDefault();
        SelectedStockLocation ??= locations.FirstOrDefault();
    }

    private async Task LoadInventoryAsync(CancellationToken cancellationToken)
    {
        _inventory = await _apiClient.GetInventoryAsync(cancellationToken);
        ApplyInventoryFilter();
        Replace(ExpiringInventory, _inventory.Where(item => item.ExpiresOn.HasValue && item.ExpiresOn.Value <= DateOnly.FromDateTime(DateTime.Today.AddDays(7))).OrderBy(item => item.ExpiresOn));
    }

    private async Task LoadShoppingAsync(CancellationToken cancellationToken) => Replace(ShoppingItems, (await _apiClient.GetShoppingListAsync(cancellationToken)).Items);

    private async Task LoadDashboardAsync(CancellationToken cancellationToken)
    {
        var dashboard = await _apiClient.GetDashboardAsync(cancellationToken);
        ProductCount = dashboard.ProductCount;
        LowStockCount = dashboard.LowStockCount;
        ExpiringCount = dashboard.ExpiringSoonCount;
        ShoppingCount = dashboard.ShoppingItemCount;
    }

    private async Task RefreshOperationalDataAsync(CancellationToken cancellationToken)
    {
        await LoadInventoryAsync(cancellationToken);
        await LoadDashboardAsync(cancellationToken);
    }

    private void ApplyProductFilter() => Replace(VisibleProducts, string.IsNullOrWhiteSpace(ProductSearch) ? _products : _products.Where(product => product.Name.Contains(ProductSearch.Trim(), StringComparison.CurrentCultureIgnoreCase) || (product.Barcode?.Contains(ProductSearch.Trim(), StringComparison.OrdinalIgnoreCase) ?? false)));
    private void ApplyInventoryFilter() => Replace(VisibleInventory, string.IsNullOrWhiteSpace(InventoryFilter) ? _inventory : _inventory.Where(lot => lot.ProductName.Contains(InventoryFilter.Trim(), StringComparison.CurrentCultureIgnoreCase) || (lot.Barcode?.Contains(InventoryFilter.Trim(), StringComparison.OrdinalIgnoreCase) ?? false) || lot.LocationName.Contains(InventoryFilter.Trim(), StringComparison.CurrentCultureIgnoreCase)));

    private void ClearProductEditor()
    {
        _editingProductId = null;
        ProductName = string.Empty;
        ProductBarcode = string.Empty;
        ProductUnit = "個";
        ReorderPoint = string.Empty;
        TargetQuantity = string.Empty;
        OnPropertyChanged(nameof(SaveProductText));
    }

    private void ClearLocationEditor()
    {
        _editingLocationId = null;
        LocationName = string.Empty;
        LocationSortOrder = "0";
        OnPropertyChanged(nameof(SaveLocationText));
    }

    private void ClearLoadedData()
    {
        _products = [];
        _inventory = [];
        VisibleProducts.Clear();
        Locations.Clear();
        VisibleInventory.Clear();
        ExpiringInventory.Clear();
        ShoppingItems.Clear();
        PrintPreview = "プレビューを表示すると、ここに印刷内容が表示されます。";
        ProductCount = LowStockCount = ExpiringCount = ShoppingCount = 0;
    }

    private Uri GetApiUri() => Uri.TryCreate(ApiUrl.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri : throw new InvalidOperationException("有効なHTTPまたはHTTPSのAPI URLを入力してください。");
    private Task<bool> ConfirmRestoreAsync(string itemType, string inputName, string deletedName) => _userInteraction.ConfirmAsync($"削除済み{itemType}の候補", $"入力した「{inputName}」に一致または部分一致する削除済み{itemType}「{deletedName}」があります。\n復元して入力内容で更新しますか？", "復元", "新規登録");
    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string FormatDecimal(decimal? value) => value?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;

    private static decimal ParsePositiveDecimal(string value, string fieldName)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var result) || result <= 0) throw new InvalidOperationException($"{fieldName}には0より大きい数値を入力してください。");
        return result;
    }

    private static decimal? ParseOptionalDecimal(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var result) || result < 0) throw new InvalidOperationException($"{fieldName}には0以上の数値を入力してください。");
        return result;
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private async Task RunUiActionAsync(Func<CancellationToken, Task> action)
    {
        try
        {
            await action(CancellationToken.None);
        }
        catch (Exception exception) when (exception is ApiException or HttpRequestException or InvalidOperationException)
        {
            Status = exception.Message;
        }
    }
}
