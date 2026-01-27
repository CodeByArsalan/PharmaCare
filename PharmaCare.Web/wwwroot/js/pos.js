// POS System JavaScript
let cartData = [];
let searchTimeout;
let invoiceDiscount = 0;
let invoiceDiscountPercent = 0;

// Initialize on page load
$(document).ready(function () {
    loadCart();
    setupSearchHandler();
    loadHeldSalesCount();
});

// Setup search with debounce
function setupSearchHandler() {
    $('#productSearch').on('input', function () {
        clearTimeout(searchTimeout);
        const query = $(this).val().trim();

        if (query.length < 2) {
            showEmptySearchMessage();
            return;
        }
        console.log("In set up search handler");
        searchTimeout = setTimeout(() => searchProducts(query), 300);
    });
}

// Search products
function searchProducts(query) {
    console.log("In search Products");
    $.ajax({
        url: '/Pos/SearchProducts',
        type: 'GET',
        data: { query: query },
        success: function (products) {
            console.log("In success");
            console.log("Products ==> ", products);
            displaySearchResults(products);
        },
        error: function () {
            showErrorMessage('Search failed. Please try again.');
        }
    });
}

// Display search results
function displaySearchResults(products) {
    console.log("Search Results:", products);

    // FIX: Define resultsContainer variable
    const resultsContainer = $('#searchResults');

    if (!products || !Array.isArray(products) || products.length === 0) {
        resultsContainer.html(`
            <div class="text-center text-muted py-5">
                <i class="bx bx-search-alt-2 bx-lg"></i>
                <p class="mt-2">No products found</p>
            </div>
        `);
        return;
    }

    let html = '<table class="table table-hover">';
    html += '<thead><tr><th>Product</th><th>Batch</th><th>Stock</th><th>Price</th><th>Action</th></tr></thead><tbody>';

    products.forEach(product => {
        // Case 1: Product has batches
        if (product.availableBatches && product.availableBatches.length > 0) {
            product.availableBatches.forEach(batch => {
                const expiryClass = batch.isExpiringSoon ? 'text-warning' : '';
                const expiryIcon = batch.isExpiringSoon ? '<i class="bx bx-error-circle"></i> ' : '';

                html += `
                    <tr>
                        <td>
                            <strong>${escapeHtml(product.brandName)}</strong><br>
                            <small class="text-muted">${escapeHtml(product.genericName || '')}</small>
                        </td>
                        <td>
                            ${escapeHtml(batch.batchNumber)}<br>
                            <small class="${expiryClass}">${expiryIcon}Exp: ${formatDate(batch.expiryDate)}</small>
                        </td>
                        <td>
                            <span class="badge ${batch.availableQuantity < 1 ? 'bg-label-danger' : (batch.availableQuantity < 10 ? 'bg-label-warning' : 'bg-label-success')}">
                                ${batch.availableQuantity} units
                            </span>
                        </td>
                        <td><strong>PKR ${batch.price.toFixed(2)}</strong></td>
                        <td>
                            ${batch.availableQuantity > 0 ?
                        `<button class="btn btn-sm btn-primary" 
                                    onclick="addToCart(${product.productID}, ${batch.productBatchID}, '${escapeHtml(product.brandName)}', '${escapeHtml(batch.batchNumber)}', ${batch.price})">
                                    <i class="bx bx-cart-add"></i> Add
                                </button>` :
                        `<button class="btn btn-sm btn-secondary" disabled>
                                    Out of Stock
                                </button>`
                    }
                        </td>
                    </tr>
                `;
            });
        }
        // Case 2: Product found but no batches (expired or unlinked)
        else {
            html += `
                <tr>
                    <td>
                        <strong>${escapeHtml(product.brandName)}</strong><br>
                        <small class="text-muted">${escapeHtml(product.genericName || '')}</small>
                    </td>
                    <td colspan="3" class="text-center text-muted">
                        <em>No valid batches found</em>
                    </td>
                    <td>
                        <button class="btn btn-sm btn-secondary" disabled>
                            Unavailable
                        </button>
                    </td>
                </tr>
            `;
        }
    });

    html += '</tbody></table>';
    resultsContainer.html(html);
}

// Add item to cart
function addToCart(productID, productBatchID, productName, batchNumber, price) {
    const quantity = 1; // Default quantity

    $.ajax({
        url: '/Pos/AddToCart',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({
            productBatchID: productBatchID,
            quantity: quantity,
            price: price
        }),
        success: function (response) {
            if (response.success) {
                loadCart();
                showSuccessMessage(`${productName} added to cart`);
            } else {
                showErrorMessage(response.message);
            }
        },
        error: function () {
            showErrorMessage('Failed to add item to cart');
        }
    });
}

// Load cart
function loadCart() {
    $.ajax({
        url: '/Pos/GetCartData',
        type: 'GET',
        success: function (response) {
            cartData = response.items || [];
            displayCart(response.items, response.total);
        },
        error: function () {
            console.error('Failed to load cart');
        }
    });
}

// Display cart
function displayCart(items, total) {
    const cartContainer = $('#cartItems');
    const cartCount = $('#cartCount');
    const cartTotal = $('#cartTotal');
    const checkoutBtn = $('#checkoutBtn');

    // Safety check - ensure items is an array
    if (!items) {
        items = [];
    }

    if (total === undefined || total === null) {
        total = 0;
    }

    cartCount.text(items.length);
    cartTotal.text(total.toFixed(2));

    if (items.length === 0) {
        cartContainer.html(`
            <div class="text-center text-muted py-5">
                <i class="bx bx-cart bx-lg"></i>
                <p class="mt-2">Your cart is empty</p>
            </div>
        `);
        checkoutBtn.prop('disabled', true);
        return;
    }

    checkoutBtn.prop('disabled', false);

    let html = '<div class="list-group list-group-flush">';
    items.forEach(item => {
        html += `
            <div class="list-group-item p-3">
                <div class="d-flex justify-content-between align-items-start mb-2">
                    <div class="flex-grow-1">
                        <h6 class="mb-0">${escapeHtml(item.productName)}</h6>
                        <small class="text-muted">Batch: ${escapeHtml(item.batchNumber)}</small><br>
                        <small class="text-muted">Exp: ${formatDate(item.expiryDate)}</small>
                    </div>
                    <button class="btn btn-sm btn-icon btn-text-secondary" 
                            onclick="removeFromCart(${item.productBatchID})">
                        <i class="bx bx-x"></i>
                    </button>
                </div>
                <div class="d-flex justify-content-between align-items-center">
                    <div class="btn-group btn-group-sm" role="group">
                        <button type="button" class="btn btn-outline-secondary" 
                                onclick="updateQuantity(${item.productBatchID}, ${item.quantity - 1})">
                            <i class="bx bx-minus"></i>
                        </button>
                        <button type="button" class="btn btn-outline-secondary" disabled>
                            ${item.quantity}
                        </button>
                        <button type="button" class="btn btn-outline-secondary" 
                                onclick="updateQuantity(${item.productBatchID}, ${item.quantity + 1})">
                            <i class="bx bx-plus"></i>
                        </button>
                    </div>
                    <div>
                        <small class="text-muted">PKR ${item.unitPrice.toFixed(2)} × ${item.quantity}</small><br>
                        <strong class="text-primary">PKR ${item.subtotal.toFixed(2)}</strong>
                    </div>
                </div>
            </div>
        `;
    });
    html += '</div>';

    cartContainer.html(html);
}

// Update item quantity
function updateQuantity(productBatchID, newQuantity) {
    if (newQuantity < 1) {
        removeFromCart(productBatchID);
        return;
    }

    $.ajax({
        url: '/Pos/UpdateCartItem',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({
            productBatchID: productBatchID,
            quantity: newQuantity
        }),
        success: function (response) {
            if (response.success) {
                loadCart();
            }
        },
        error: function () {
            showErrorMessage('Failed to update quantity');
        }
    });
}

// Remove item from cart
function removeFromCart(productBatchID) {
    $.ajax({
        url: '/Pos/RemoveFromCart',
        type: 'POST',
        data: { productBatchID: productBatchID },
        success: function (response) {
            if (response.success) {
                loadCart();
                showSuccessMessage('Item removed from cart');
            }
        },
        error: function () {
            showErrorMessage('Failed to remove item');
        }
    });
}

// Clear cart
function clearCart() {
    if (!confirm('Are you sure you want to clear the cart?')) {
        return;
    }

    $.ajax({
        url: '/Pos/ClearCart',
        type: 'POST',
        success: function (response) {
            if (response.success) {
                loadCart();
                showSuccessMessage('Cart cleared');
            }
        },
        error: function () {
            showErrorMessage('Failed to clear cart');
        }
    });
}

// Open checkout modal
function openCheckoutModal() {
    const total = parseFloat($('#cartTotal').text());
    $('#checkoutTotal').text(total.toFixed(2));
    $('#checkoutItemCount').text(cartData.length);
    $('#amountTendered').val(total.toFixed(2));
    calculateChange();

    // Reset customer fields
    $('#typeWalkIn').prop('checked', true);
    toggleCustomerType();
    loadCustomers();

    const modal = new bootstrap.Modal(document.getElementById('checkoutModal'));
    modal.show();
}

// Load customers for dropdown
function loadCustomers() {
    $.ajax({
        url: '/Pos/GetCustomers',
        type: 'GET',
        success: function (data) {
            const select = $('#customerSelect');
            select.empty();
            select.append('<option value="">Select Customer...</option>');
            data.forEach(c => {
                select.append(`<option value="${c.id}">${c.text}</option>`);
            });
        }
    });
}

// Toggle Customer Logic
function toggleCustomerType() {
    const isWalkIn = $('#typeWalkIn').is(':checked');
    if (isWalkIn) {
        $('#existingCustomerDiv').addClass('d-none');
        $('#customerInfoDiv input').prop('disabled', false).val('');
        $('#nameOptionalLabel').removeClass('d-none');
        $('#phoneOptionalLabel').removeClass('d-none');
    } else {
        $('#existingCustomerDiv').removeClass('d-none');
        // Clear manual inputs when existing is selected
        $('#customerInfoDiv input').prop('disabled', true).val('');
        $('#nameOptionalLabel').addClass('d-none');
        $('#phoneOptionalLabel').addClass('d-none');
    }
}

// Calculate change
function calculateChange() {
    const total = parseFloat($('#cartTotal').text());
    const tendered = parseFloat($('#amountTendered').val()) || 0;
    const change = tendered - total;

    if (change >= 0) {
        $('#changeAmount').text(change.toFixed(2));
        $('#changeAlert').removeClass('d-none');
    } else {
        $('#changeAlert').addClass('d-none');
    }
}

// Complete sale
function completeSale() {
    const total = parseFloat($('#cartTotal').text());
    const tendered = parseFloat($('#amountTendered').val()) || 0;

    const isWalkIn = $('#typeWalkIn').is(':checked');
    let customerID = null;
    let customerName = $('#customerName').val();
    let customerPhone = $('#customerPhone').val();

    if (tendered < total) {
        // Enforce customer identification for credit sales
        if (isWalkIn && !customerName) {
            showErrorMessage('Credit sales require a customer name');
            return;
        }
        if (!isWalkIn && !$('#customerSelect').val()) {
            showErrorMessage('Please select a customer for this credit sale');
            return;
        }
    }

    if (!isWalkIn) {
        customerID = $('#customerSelect').val();
    }

    const checkoutData = {
        customerName: customerName,
        customerPhone: customerPhone,
        customerID: customerID ? parseInt(customerID) : null,
        payments: [{
            paymentMethod: $('#paymentMethod').val(),
            amount: tendered // Send the actual amount paid, not the total
        }]
    };

    $.ajax({
        url: '/Pos/Checkout',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(checkoutData),
        beforeSend: function () {
            $('#checkoutModal .btn-primary').prop('disabled', true);
        },
        success: function (response) {
            if (response.success) {
                window.location.href = `/Pos/Receipt/${response.saleId}`;
            } else {
                showErrorMessage(response.message);
                $('#checkoutModal .btn-primary').prop('disabled', false);
            }
        },
        error: function () {
            showErrorMessage('Checkout failed. Please try again.');
            $('#checkoutModal .btn-primary').prop('disabled', false);
        }
    });
}

// Helper functions
function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-GB');
}

function escapeHtml(text) {
    if (!text) return '';
    const map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return text.toString().replace(/[&<>"']/g, m => map[m]);
}

function showEmptySearchMessage() {
    $('#searchResults').html(`
        <div class="text-center text-muted py-5">
            <i class="bx bx-search-alt bx-lg"></i>
            <p class="mt-2">Start typing to search for products</p>
        </div>
    `);
}

function showSuccessMessage(message) {
    console.log('Success:', message);
}

function showErrorMessage(message) {
    alert(message);
}

// ==========================================
// HOLD/PARK SALE FUNCTIONALITY
// ==========================================

// Load held sales count
function loadHeldSalesCount() {
    $.ajax({
        url: '/HeldSale/GetHeldSalesCount',
        type: 'GET',
        success: function (response) {
            $('#heldSalesCount').text(response.count || 0);
        },
        error: function () {
            console.log('Failed to load held sales count');
        }
    });
}

// Open hold sale modal
function openHoldModal() {
    $('#holdCustomerName').val('');
    $('#holdCustomerPhone').val('');
    $('#holdNotes').val('');
    $('#holdItemCount').text(cartData.length);
    new bootstrap.Modal(document.getElementById('holdModal')).show();
}

// Hold current sale
function holdSale() {
    if (cartData.length === 0) {
        showErrorMessage('Cart is empty');
        return;
    }

    const holdData = {
        customerName: $('#holdCustomerName').val(),
        customerPhone: $('#holdCustomerPhone').val(),
        notes: $('#holdNotes').val(),
        items: cartData.map(item => ({
            productID: item.productID,
            productBatchID: item.productBatchID,
            quantity: item.quantity,
            unitPrice: item.unitPrice,
            discountPercent: item.discountPercent || 0,
            discountAmount: item.discountAmount || 0
        }))
    };

    $.ajax({
        url: '/HeldSale/HoldSale',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(holdData),
        success: function (response) {
            if (response.success) {
                bootstrap.Modal.getInstance(document.getElementById('holdModal')).hide();
                clearCartAfterHold();
                loadHeldSalesCount();
                showSuccessMessage('Sale parked successfully');
                alert('Sale parked successfully! You can resume it later from Held Sales.');
            } else {
                showErrorMessage(response.message);
            }
        },
        error: function () {
            showErrorMessage('Failed to hold sale');
        }
    });
}

// Clear cart after hold (without confirmation)
function clearCartAfterHold() {
    $.ajax({
        url: '/Pos/ClearCart',
        type: 'POST',
        success: function () {
            loadCart();
        }
    });
}

// ==========================================
// DISCOUNT FUNCTIONALITY
// ==========================================

// Calculate discount from percentage
function calculateDiscount() {
    const subtotal = parseFloat($('#cartTotal').text()) || 0;
    const percent = parseFloat($('#discountPercent').val()) || 0;

    if (percent > 100 || percent < 0) {
        $('#discountPercent').val(0);
        return;
    }

    invoiceDiscountPercent = percent;
    invoiceDiscount = (subtotal * percent) / 100;

    $('#discountAmount').val(invoiceDiscount.toFixed(2));
    updateNetTotal();
}

// Calculate discount from amount
function calculateDiscountFromAmount() {
    const subtotal = parseFloat($('#cartTotal').text()) || 0;
    const amount = parseFloat($('#discountAmount').val()) || 0;

    if (amount > subtotal || amount < 0) {
        $('#discountAmount').val(0);
        return;
    }

    invoiceDiscount = amount;
    invoiceDiscountPercent = subtotal > 0 ? (amount / subtotal) * 100 : 0;

    $('#discountPercent').val(invoiceDiscountPercent.toFixed(2));
    updateNetTotal();
}

// Update net total after discount
function updateNetTotal() {
    const subtotal = parseFloat($('#cartTotal').text()) || 0;
    const netTotal = subtotal - invoiceDiscount;

    $('#netTotal').val('Rs. ' + netTotal.toFixed(2));
    $('#amountTendered').val(netTotal.toFixed(2));
    $('#checkoutTotal').text(netTotal.toFixed(2));
    calculateChange();
}

// ==========================================
// UPDATED CART DISPLAY
// ==========================================

// Update displayCart to show subtotal and enable hold button
const originalDisplayCart = displayCart;
displayCart = function (items, total) {
    const cartContainer = $('#cartItems');
    const cartCount = $('#cartCount');
    const cartTotal = $('#cartTotal');
    const cartSubtotal = $('#cartSubtotal');
    const cartDiscount = $('#cartDiscount');
    const checkoutBtn = $('#checkoutBtn');
    const holdBtn = $('#holdBtn');

    // Safety check
    if (!items) items = [];
    if (total === undefined || total === null) total = 0;

    cartCount.text(items.length);
    cartTotal.text(total.toFixed(2));
    cartSubtotal.text(total.toFixed(2));
    cartDiscount.text('0.00'); // Reset discount on cart refresh
    invoiceDiscount = 0;
    invoiceDiscountPercent = 0;

    if (items.length === 0) {
        cartContainer.html(`
            <div class="text-center text-muted py-5">
                <i class="bx bx-cart bx-lg"></i>
                <p class="mt-2">Your cart is empty</p>
            </div>
        `);
        checkoutBtn.prop('disabled', true);
        holdBtn.prop('disabled', true);
        return;
    }

    checkoutBtn.prop('disabled', false);
    holdBtn.prop('disabled', false);

    let html = '<div class="list-group list-group-flush">';
    items.forEach(item => {
        html += `
            <div class="list-group-item p-3">
                <div class="d-flex justify-content-between align-items-start mb-2">
                    <div class="flex-grow-1">
                        <h6 class="mb-0">${escapeHtml(item.productName)}</h6>
                        <small class="text-muted">Batch: ${escapeHtml(item.batchNumber)}</small><br>
                        <small class="text-muted">Exp: ${formatDate(item.expiryDate)}</small>
                    </div>
                    <button class="btn btn-sm btn-icon btn-text-secondary" 
                            onclick="removeFromCart(${item.productBatchID})">
                        <i class="bx bx-x"></i>
                    </button>
                </div>
                <div class="d-flex justify-content-between align-items-center">
                    <div class="btn-group btn-group-sm" role="group">
                        <button type="button" class="btn btn-outline-secondary" 
                                onclick="updateQuantity(${item.productBatchID}, ${item.quantity - 1})">
                            <i class="bx bx-minus"></i>
                        </button>
                        <button type="button" class="btn btn-outline-secondary" disabled>
                            ${item.quantity}
                        </button>
                        <button type="button" class="btn btn-outline-secondary" 
                                onclick="updateQuantity(${item.productBatchID}, ${item.quantity + 1})">
                            <i class="bx bx-plus"></i>
                        </button>
                    </div>
                    <div>
                        <small class="text-muted">PKR ${item.unitPrice.toFixed(2)} × ${item.quantity}</small><br>
                        <strong class="text-primary">PKR ${item.subtotal.toFixed(2)}</strong>
                    </div>
                </div>
            </div>
        `;
    });
    html += '</div>';

    cartContainer.html(html);
};

// ==========================================
// UPDATED CHECKOUT
// ==========================================

// Override openCheckoutModal to include discount
const originalOpenCheckoutModal = openCheckoutModal;
openCheckoutModal = function () {
    const total = parseFloat($('#cartTotal').text());
    $('#checkoutTotal').text(total.toFixed(2));
    $('#checkoutItemCount').text(cartData.length);

    // Reset discount fields
    $('#discountPercent').val('');
    $('#discountAmount').val('');
    $('#netTotal').val('Rs. ' + total.toFixed(2));
    $('#amountTendered').val(total.toFixed(2));
    $('#referenceNumber').val('');

    invoiceDiscount = 0;
    invoiceDiscountPercent = 0;

    calculateChange();

    // Reset customer fields
    $('#typeWalkIn').prop('checked', true);
    toggleCustomerType();
    loadCustomers();

    const modal = new bootstrap.Modal(document.getElementById('checkoutModal'));
    modal.show();
};

// Update completeSale to include discount
const originalCompleteSale = completeSale;
completeSale = function () {
    const subtotal = parseFloat($('#cartTotal').text());
    const netTotal = subtotal - invoiceDiscount;
    const tendered = parseFloat($('#amountTendered').val()) || 0;

    const isWalkIn = $('#typeWalkIn').is(':checked');
    let customerID = null;
    let customerName = $('#customerName').val();
    let customerPhone = $('#customerPhone').val();

    if (tendered < netTotal) {
        // Enforce customer identification for credit sales
        if (isWalkIn && !customerName) {
            showErrorMessage('Credit sales require a customer name');
            return;
        }
        if (!isWalkIn && !$('#customerSelect').val()) {
            showErrorMessage('Please select a customer for this credit sale');
            return;
        }
    }

    if (!isWalkIn) {
        customerID = $('#customerSelect').val();
    }

    const checkoutData = {
        customerName: customerName,
        customerPhone: customerPhone,
        customerID: customerID ? parseInt(customerID) : null,
        discountPercent: invoiceDiscountPercent,
        discountAmount: invoiceDiscount,
        referenceNumber: $('#referenceNumber').val(),
        payments: [{
            paymentMethod: $('#paymentMethod').val(),
            amount: tendered,
            referenceNumber: $('#referenceNumber').val()
        }]
    };

    $.ajax({
        url: '/Pos/Checkout',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(checkoutData),
        beforeSend: function () {
            $('#checkoutModal .btn-primary').prop('disabled', true);
        },
        success: function (response) {
            if (response.success) {
                window.location.href = `/Pos/Receipt/${response.saleId}`;
            } else {
                showErrorMessage(response.message);
                $('#checkoutModal .btn-primary').prop('disabled', false);
            }
        },
        error: function () {
            showErrorMessage('Checkout failed. Please try again.');
            $('#checkoutModal .btn-primary').prop('disabled', false);
        }
    });
};

// Update calculateChange to use net total
const originalCalculateChange = calculateChange;
calculateChange = function () {
    const subtotal = parseFloat($('#cartTotal').text()) || 0;
    const discount = parseFloat($('#discountAmount').val()) || 0;
    const netTotal = subtotal - discount;
    const tendered = parseFloat($('#amountTendered').val()) || 0;
    const change = tendered - netTotal;

    if (change >= 0) {
        $('#changeAmount').text(change.toFixed(2));
        $('#changeAlert').removeClass('d-none');
    } else {
        $('#changeAlert').addClass('d-none');
    }
};