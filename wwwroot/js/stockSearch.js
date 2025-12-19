document.getElementById('stockSearch').addEventListener('input', async function () {
    const query = this.value.toLowerCase();
    if (!query) {
        document.getElementById('stockResults').innerHTML = '';
        return;
    }
    const res = await fetch('/Stock/GetStockTickers');
    const stocks = await res.json();
    const matches = stocks.filter(ticker => ticker.toLowerCase().includes(query));
    document.getElementById('stockResults').innerHTML = matches
        .map(ticker => `<li onclick="selectStock('${ticker}')">${ticker}</li>`)
        .join('');
});

function selectStock(ticker) {
    document.getElementById('stockSearch').value = ticker;
    document.querySelector('input[name="ticker"]').value = ticker;
    document.getElementById('stockResults').innerHTML = '';
    document.querySelector('form').submit();
}