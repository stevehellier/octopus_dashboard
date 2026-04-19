window.octopusCharts = {
    instances: {},

    render(canvasId, labels, data, label, color, prevData, prevLabel) {
        if (this.instances[canvasId]) {
            this.instances[canvasId].destroy();
            delete this.instances[canvasId];
        }

        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        const datasets = [
            {
                type: 'bar',
                label,
                data,
                backgroundColor: color + '55',
                borderColor: color,
                borderWidth: 1,
                borderRadius: 3,
                order: 2
            }
        ];

        if (prevData && prevData.length > 0) {
            datasets.push({
                type: 'line',
                label: prevLabel,
                data: prevData,
                borderColor: '#adb5bd',
                backgroundColor: 'transparent',
                borderWidth: 2,
                borderDash: [6, 4],
                pointRadius: 2,
                pointBackgroundColor: '#adb5bd',
                tension: 0.1,
                order: 1
            });
        }

        this.instances[canvasId] = new Chart(canvas, {
            data: { labels, datasets },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { position: 'top' },
                    tooltip: {
                        callbacks: {
                            label: ctx => `${ctx.dataset.label}: ${ctx.parsed.y.toFixed(3)}`
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        title: { display: true, text: label }
                    },
                    x: {
                        ticks: { maxRotation: 45, autoSkip: true, maxTicksLimit: 31 }
                    }
                }
            }
        });
    }
};
