function renderCandlestickChart(dataPoints, sma, smaPeriod, ema, emaPeriod, signals, rsi, rsiPeriod, macdLine, macdSignal, macdHistogram) {
    const ctx = document.getElementById('candlestickChart').getContext('2d');
    if (!dataPoints || !Array.isArray(dataPoints) || dataPoints.length === 0) {
        console.warn('No candlestick dataPoints provided');
        return;
    }

    // Group candles by day
    const groupedByDay = {};
    dataPoints.forEach((dp, i) => {
        const day = new Date(dp.x ? dp.x : dp.Date).toISOString().slice(0, 10); // yyyy-mm-dd
        if (!groupedByDay[day]) groupedByDay[day] = [];
        groupedByDay[day].push({ ...dp, _index: i }); // keep index for indicators
    });
    const days = Object.keys(groupedByDay).sort();
    let currentDayIndex = days.length - 1; // default to latest day

    function drawChartForDay(dayIndex) {
        const day = days[dayIndex];
        const candles = groupedByDay[day].map(dp => ({
            x: dp.x ? new Date(dp.x) : new Date(dp.Date),
            o: dp.o ?? dp.Open,
            h: dp.h ?? dp.High,
            l: dp.l ?? dp.Low,
            c: dp.c ?? dp.Close
        }));
        document.getElementById('currentDayLabel').textContent = day;
        if (window.candlestickChartInstance) {
            window.candlestickChartInstance.destroy();
        }
        window.candlestickChartInstance = new Chart(ctx, {
            type: 'candlestick',
            data: {
                datasets: [{
                    label: 'Candlestick',
                    data: candles,
                    color: {
                        up: '#26a69a',
                        down: '#ef5350',
                        unchanged: '#ccc'
                    },
                    barPercentage: 0.2,
                    categoryPercentage: 0.1
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: { display: false }
                },
                scales: {
                    x: {
                        type: 'time',
                        time: {
                            tooltipFormat: 'HH:mm',
                            unit: 'minute',
                            displayFormats: {
                                minute: 'HH:mm'
                            }
                        },
                        title: { display: true, text: 'Time' }
                    },
                    y: { display: true, title: { display: true, text: 'Price' } }
                }
            }
        });
        // Filter indicators for the same day
        const indices = groupedByDay[day].map(dp => dp._index);
        const daySMA = sma ? indices.map(i => sma[i]) : null;
        const dayEMA = ema ? indices.map(i => ema[i]) : null;
        const dayRSI = rsi ? indices.map(i => rsi[i]) : null;
        const dayMACDLine = macdLine ? indices.map(i => macdLine[i]) : null;
        const dayMACDSignal = macdSignal ? indices.map(i => macdSignal[i]) : null;
        const dayMACDHistogram = macdHistogram ? indices.map(i => macdHistogram[i]) : null;
        // Calculate buy/sell signals for the selected day
        let daySignals = [];
        if (daySMA && dayEMA && daySMA.length === dayEMA.length) {
            for (let i = 1; i < daySMA.length; i++) {
                if (dayEMA[i] != null && daySMA[i] != null && dayEMA[i-1] != null && daySMA[i-1] != null) {
                    if (dayEMA[i] > daySMA[i] && dayEMA[i-1] <= daySMA[i-1]) {
                        daySignals.push({
                            Type: 'Buy',
                            x: candles[i].x,
                            Price: candles[i].c
                        });
                    } else if (dayEMA[i] < daySMA[i] && dayEMA[i-1] >= daySMA[i-1]) {
                        daySignals.push({
                            Type: 'Sell',
                            x: candles[i].x,
                            Price: candles[i].c
                        });
                    }
                }
            }
        }
        // Update other charts for the same day
        renderStockChart(groupedByDay[day], daySMA, smaPeriod, dayEMA, emaPeriod, daySignals);
        renderRSIChart(dayRSI, rsiPeriod, groupedByDay[day].map(dp => dp.x ? dp.x : dp.Date));
        renderMACDChart(dayMACDLine, dayMACDSignal, dayMACDHistogram, groupedByDay[day].map(dp => dp.x ? dp.x : dp.Date));
    }

    // Initial draw
    drawChartForDay(currentDayIndex);

    // Navigation buttons
    document.getElementById('prevDayBtn').onclick = function() {
        if (currentDayIndex > 0) {
            currentDayIndex--;
            drawChartForDay(currentDayIndex);
        }
    };
    document.getElementById('nextDayBtn').onclick = function() {
        if (currentDayIndex < days.length - 1) {
            currentDayIndex++;
            drawChartForDay(currentDayIndex);
        }
    };
}

function renderStockChart(dataPoints, sma, smaPeriod, ema, emaPeriod, signals) {
    const ctx = document.getElementById('stockChart').getContext('2d');
    if (!dataPoints || !Array.isArray(dataPoints) || dataPoints.length === 0) {
        console.warn('No stock dataPoints provided');
        return;
    }
    const labels = dataPoints.map(dp => dp.x ? new Date(dp.x) : (dp.Date ? new Date(dp.Date) : dp.TimeLabel));
    const closeData = dataPoints.map(dp => dp.c ?? dp.Close ?? dp.close);
    const datasets = [{
        label: 'Close Price',
        data: closeData,
        borderColor: 'rgba(75, 80, 80, 1)',
        borderWidth: 1,
        backgroundColor: 'rgba(75, 80, 80, 0.2)',
        fill: false,
        spanGaps: true,
        tension: 0.1,
        pointRadius: 1.5,
        pointBackgroundColor: 'rgba(75, 80, 80, 1)'
    }];
    if (sma && Array.isArray(sma)) {
        datasets.push({
            label: `SMA (${smaPeriod})`,
            data: sma,
            borderColor: 'rgba(255, 99, 132, 1)',
            borderWidth: 2,
            fill: false,
            spanGaps: true,
            pointRadius: 0,
            tension: 0.1
        });
    }
    if (ema && Array.isArray(ema)) {
        datasets.push({
            label: `EMA (${emaPeriod})`,
            data: ema,
            borderColor: 'rgba(54, 162, 235, 1)',
            borderWidth: 2,
            fill: false,
            spanGaps: true,
            pointRadius: 0,
            borderDash: [5, 5],
            tension: 0.1
        });
    }
    if (signals && Array.isArray(signals)) {
        const buySignals = signals.filter(s => s.Type === "Buy");
        const sellSignals = signals.filter(s => s.Type === "Sell");
        datasets.push({
            label: 'Buy Signal',
            data: buySignals.map(s => ({ x: s.x ? new Date(s.x) : (s.Date ? new Date(s.Date) : s.TimeLabel), y: s.Price })),
            pointStyle: 'triangle',
            pointRadius: 10,
            showLine: false,
            backgroundColor: 'green',
            borderColor: 'green',
            type: 'scatter'
        });
        datasets.push({
            label: 'Sell Signal',
            data: sellSignals.map(s => ({ x: s.x ? new Date(s.x) : (s.Date ? new Date(s.Date) : s.TimeLabel), y: s.Price })),
            pointStyle: 'rectRot',
            pointRadius: 10,
            showLine: false,
            backgroundColor: 'red',
            borderColor: 'red',
            type: 'scatter'
        });
    }
    if (window.stockChartInstance) {
        window.stockChartInstance.destroy();
    }
    window.stockChartInstance = new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: datasets
        },
        options: {
            responsive: true,
            plugins: {
                tooltip: {
                    mode: 'index',
                    intersect: true
                }
            },
            scales: {
                x: {
                    type: 'time',
                    time: {
                        tooltipFormat: 'HH:mm',
                        unit: 'minute',
                        displayFormats: {
                            minute: 'HH:mm'
                        }
                    },
                    title: { display: true, text: 'Time' }
                },
                y: {
                    display: true,
                    title: { display: true, text: 'Price' }
                }
            }
        }
    });
}

function renderRSIChart(rsi, rsiPeriod, labels) {
    if (!rsi || !Array.isArray(rsi) || !labels || labels.length === 0) return;
    const ctx = document.getElementById('rsiChart').getContext('2d');
    const isoLabels = labels.map(l => l ? new Date(l) : null);
    new Chart(ctx, {
        type: 'line',
        data: {
            labels: isoLabels,
            datasets: [{
                label: `RSI (${rsiPeriod})`,
                data: rsi,
                borderColor: 'orange',
                fill: false,
                spanGaps: true,
                pointRadius: 0,
                tension: 0.1
            }]
        },
        options: {
            responsive: true,
            plugins: {
                tooltip: { mode: 'index', intersect: false },
                annotation: {
                    annotations: {
                        highlight: {
                            type: 'box',
                            xMin: null,
                            xMax: null,
                            yMin: 30,
                            yMax: 70,
                            backgroundColor: 'rgba(40,60,120,0.6)',
                            borderWidth: 0
                        }
                    }
                }
            },
            scales: {
                x: { type: 'time', display: true, title: { display: true, text: 'Time' } },
                y: { display: true, min: 30, max: 70, title: { display: true, text: 'RSI' } }
            }
        }
    });
}

function renderMACDChart(macdLine, macdSignal, macdHistogram, labels) {
    if (!macdLine || !macdSignal || !macdHistogram || !labels || labels.length === 0) return;
    const ctx = document.getElementById('macdChart').getContext('2d');
    const isoLabels = labels.map(l => l ? new Date(l) : null);
    new Chart(ctx, {
        type: 'bar',
        data: {
            labels: isoLabels,
            datasets: [
                {
                    type: 'line',
                    label: 'MACD Line',
                    data: macdLine,
                    borderColor: 'purple',
                    fill: false,
                    spanGaps: true,
                    pointRadius: 0,
                    tension: 0.1
                },
                {
                    type: 'line',
                    label: 'Signal Line',
                    data: macdSignal,
                    borderColor: 'green',
                    fill: false,
                    spanGaps: true,
                    pointRadius: 0,
                    tension: 0.1
                },
                {
                    type: 'bar',
                    label: 'Histogram',
                    data: macdHistogram,
                    backgroundColor: 'rgba(100,100,100,0.5)'
                }
            ]
        },
        options: {
            responsive: true,
            plugins: {
                tooltip: { mode: 'index', intersect: false }
            },
            scales: {
                x: { type: 'time', display: true, title: { display: true, text: 'Time' } },
                y: { display: true, title: { display: true, text: 'MACD' } }
            }
        }
    });
}

const sampleDataPoints = [
  { x: '2024-06-21T10:00:00Z', o: 100, h: 110, l: 95, c: 105 },
  { x: '2024-06-21T10:05:00Z', o: 105, h: 115, l: 100, c: 110 }
];
//renderCandlestickChart(sampleDataPoints);