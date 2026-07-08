
window.chartInterop = {
    _charts: {},

    createLineChart: function (canvasId, labels, datasets) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        if (this._charts[canvasId]) {
            this._charts[canvasId].destroy();
        }
        this._charts[canvasId] = new Chart(canvas, {
            type: 'line',
            data: {
                labels: labels,
                datasets: datasets.map(ds => ({
                    label: ds.label,
                    data: ds.data,
                    borderColor: ds.color,
                    backgroundColor: ds.color + '18',
                    tension: 0.4,
                    fill: true,
                    pointBackgroundColor: ds.color,
                    pointRadius: 4,
                    pointHoverRadius: 6,
                    borderWidth: 2.5,
                }))
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'top',
                        align: 'end',
                        labels: {
                            usePointStyle: true,
                            pointStyle: 'circle',
                            font: { family: 'Inter', size: 12, weight: '600' },
                            color: '#64748B',
                            padding: 20,
                        }
                    },
                    tooltip: {
                        backgroundColor: '#0F172A',
                        titleFont: { family: 'Inter', size: 13, weight: '700' },
                        bodyFont: { family: 'Inter', size: 12 },
                        padding: 12,
                        cornerRadius: 8,
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: { font: { family: 'Inter', size: 12 }, color: '#94A3B8' },
                        border: { display: false }
                    },
                    y: {
                        grid: { color: '#F1F5F9', lineWidth: 1 },
                        ticks: { font: { family: 'Inter', size: 12 }, color: '#94A3B8', stepSize: 1 },
                        border: { display: false },
                        beginAtZero: true,
                    }
                }
            }
        });
    },

    createDonutChart: function (canvasId, labels, data, colors) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        if (this._charts[canvasId]) {
            this._charts[canvasId].destroy();
        }
        this._charts[canvasId] = new Chart(canvas, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    backgroundColor: colors,
                    borderWidth: 3,
                    borderColor: '#FFFFFF',
                    hoverBorderWidth: 4,
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '72%',
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            usePointStyle: true,
                            pointStyle: 'circle',
                            font: { family: 'Inter', size: 12, weight: '600' },
                            color: '#64748B',
                            padding: 16,
                        }
                    },
                    tooltip: {
                        backgroundColor: '#0F172A',
                        titleFont: { family: 'Inter', size: 13, weight: '700' },
                        bodyFont: { family: 'Inter', size: 12 },
                        padding: 12,
                        cornerRadius: 8,
                    }
                }
            }
        });
    },

    destroyChart: function (canvasId) {
        if (this._charts[canvasId]) {
            this._charts[canvasId].destroy();
            delete this._charts[canvasId];
        }
    }
};
