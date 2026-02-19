    private async Task ValidateSaleAsync(StockMain sale)
    {
        // 1. Stock Validation
        var productIds = sale.StockDetails.Select(d => d.Product_ID).Distinct().ToList();
        var stockStatus = await _productService.GetStockStatusAsync(productIds);

        foreach (var detail in sale.StockDetails)
        {
            if (stockStatus.TryGetValue(detail.Product_ID, out var currentStock))
            {
                if (detail.Quantity > currentStock)
                {
                    var product = await _productRepository.GetByIdAsync(detail.Product_ID);
                    throw new InvalidOperationException(
                        $"Insufficient stock for product '{product?.Name}'. " +
                        $"Requested: {detail.Quantity}, Available: {currentStock}");
                }
            }
        }

        // 2. Walking Customer Validation
        // Identify if this is a Walking Customer transaction
        bool isWalkingCustomer = false;

        if (sale.Party_ID.HasValue)
        {
            var party = await _partyService.GetByIdWithAccountAsync(sale.Party_ID.Value);
            if (party != null && party.Account != null)
            {
                if (party.Account.AccountID == _systemAccounts.WalkingCustomerAccountId)
                {
                    isWalkingCustomer = true;
                }
                // Also check by name as fallback/secondary check if configured that way
                else if (party.Name.ToLower().Contains("walking") || party.Name.ToLower().Contains("walk-in"))
                {
                    isWalkingCustomer = true;
                }
            }
        }
        else
        {
            // No party selected implies walking customer in some flows, but usually Party_ID is required.
            // If the logic in CreateSaleVoucherAsync defaults to walking customer when null, we should check that too.
            // However, the controller seems to require Party_ID.
            // Let's assume if Party_ID is null, it might be treated as walking customer internally.
            isWalkingCustomer = true; 
        }

        if (isWalkingCustomer)
        {
            // Small tolerance for floating point issues, though currency should be precise
            if (Math.Abs(sale.TotalAmount - sale.PaidAmount) > 0.01m)
            {
                throw new InvalidOperationException(
                    "Walking Customers must pay the full amount immediately. " +
                    $"Total: {sale.TotalAmount}, Paid: {sale.PaidAmount}");
            }
        }
    }
