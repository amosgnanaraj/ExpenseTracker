// Theme toggle logic and Chart.js integration
document.addEventListener('DOMContentLoaded', () => {
    const toggleBtn = document.getElementById('themeToggleBtn');
    const toggleIcon = document.getElementById('themeToggleIcon');

    if (toggleBtn && toggleIcon) {
        // Update the theme icon based on current data-theme
        const updateIcon = (theme) => {
            if (theme === 'dark') {
                toggleIcon.className = 'bi bi-sun-fill fs-5 text-warning';
            } else {
                toggleIcon.className = 'bi bi-moon-stars-fill fs-5 text-secondary';
            }
        };

        // Initialize state
        const currentTheme = document.documentElement.getAttribute('data-theme') || 'light';
        updateIcon(currentTheme);

        // Toggle click handler
        toggleBtn.addEventListener('click', () => {
            const oldTheme = document.documentElement.getAttribute('data-theme') || 'light';
            const newTheme = oldTheme === 'dark' ? 'light' : 'dark';
            
            document.documentElement.setAttribute('data-theme', newTheme);
            localStorage.setItem('theme', newTheme);
            updateIcon(newTheme);
            
            // Dispatch a global event so that other elements (like Chart.js) can react
            const themeEvent = new CustomEvent('themechanged', { detail: { theme: newTheme } });
            window.dispatchEvent(themeEvent);
        });
    }
});

// Real-time Chart.js updating logic
window.addEventListener('themechanged', (e) => {
    const isDark = e.detail.theme === 'dark';
    const textColor = isDark ? '#cbd5e1' : '#64748b';
    const gridColor = isDark ? 'rgba(255, 255, 255, 0.08)' : 'rgba(0, 0, 0, 0.05)';

    if (typeof Chart !== 'undefined') {
        // Update global defaults for future chart renders
        if (Chart.defaults) {
            Chart.defaults.color = textColor;
            if (Chart.defaults.scale && Chart.defaults.scale.grid) {
                Chart.defaults.scale.grid.color = gridColor;
            }
        }

        // Update all existing active chart instances
        if (Chart.instances) {
            Object.values(Chart.instances).forEach(chart => {
                // Update scales colors
                if (chart.options.scales) {
                    Object.values(chart.options.scales).forEach(scale => {
                        if (scale.ticks) {
                            scale.ticks.color = textColor;
                        }
                        if (scale.grid) {
                            scale.grid.color = gridColor;
                        }
                    });
                }
                
                // Update legends colors
                if (chart.options.plugins && chart.options.plugins.legend) {
                    if (chart.options.plugins.legend.labels) {
                        chart.options.plugins.legend.labels.color = textColor;
                    }
                }

                // Update charts update
                chart.update();
            });
        }
    }
});

// Debt Reduction Simulator Logic
document.addEventListener('DOMContentLoaded', () => {
    const debtDataScript = document.getElementById('debtData');
    const extraPaymentInput = document.getElementById('extraPayment');
    const expectedReturnInput = document.getElementById('expectedReturn');
    const optimizerCallout = document.getElementById('optimizerCallout');
    const optimizerMessage = document.getElementById('optimizerMessage');

    if (debtDataScript && extraPaymentInput && expectedReturnInput) {
        let debts = [];
        try {
            debts = JSON.parse(debtDataScript.textContent);
        } catch (e) {
            console.error("Failed to parse debt data", e);
            return;
        }

        const formatCurrency = (val) => new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(val);

        const simulate = (strategy, extraPaymentOverride = null) => {
            let currentDebts = debts.map(d => {
                let monthlyInterest = (d.InterestRate / 100 / 12) * d.Balance;
                let minPay = d.MinimumPayment;
                if (minPay <= 0) {
                    minPay = monthlyInterest + (d.Balance * 0.01);
                }
                return {
                    ...d,
                    InitialBalance: d.Balance,
                    FixedMinPay: minPay
                };
            });
            
            if (strategy === 'avalanche') {
                currentDebts.sort((a, b) => b.InterestRate - a.InterestRate);
            } else if (strategy === 'snowball') {
                currentDebts.sort((a, b) => a.Balance - b.Balance);
            }

            let extraPayment = extraPaymentOverride !== null ? extraPaymentOverride : (parseFloat(extraPaymentInput.value) || 0);
            let totalMonthlyBudget = extraPayment + currentDebts.reduce((sum, d) => sum + d.FixedMinPay, 0);

            let totalMonths = 0;
            let totalInterest = 0;
            let allPaid = false;
            let balanceHistory = [];

            balanceHistory.push(currentDebts.reduce((sum, d) => sum + d.Balance, 0));
            const MAX_MONTHS = 1200;

            while (!allPaid && totalMonths < MAX_MONTHS) {
                totalMonths++;
                let availableBudget = totalMonthlyBudget;
                allPaid = true;
                
                for (let i = 0; i < currentDebts.length; i++) {
                    let debt = currentDebts[i];
                    if (debt.Balance > 0) {
                        allPaid = false;
                        let monthlyInterest = (debt.InterestRate / 100 / 12) * debt.Balance;
                        totalInterest += monthlyInterest;
                        debt.Balance += monthlyInterest;
                        
                        let minPayToApply = Math.min(debt.FixedMinPay, debt.Balance);
                        debt.Balance -= minPayToApply;
                        availableBudget -= minPayToApply;
                    }
                }

                let monthlyExtra = availableBudget;
                if (!allPaid && monthlyExtra > 0) {
                    for (let i = 0; i < currentDebts.length; i++) {
                        let debt = currentDebts[i];
                        if (debt.Balance > 0) {
                            let extraToApply = Math.min(debt.Balance, monthlyExtra);
                            debt.Balance -= extraToApply;
                            monthlyExtra -= extraToApply;
                            if (monthlyExtra <= 0) break;
                        }
                    }
                }
                
                let monthBalance = currentDebts.reduce((sum, d) => sum + d.Balance, 0);
                balanceHistory.push(monthBalance);
                
                if (monthBalance <= 0) {
                    allPaid = true;
                }
            }
            
            return {
                months: totalMonths < MAX_MONTHS ? totalMonths : '> 100 Years',
                interest: totalInterest,
                history: balanceHistory
            };
        };

        const simulateWealth = (strategy) => {
            let currentDebts = debts.map(d => {
                let monthlyInterest = (d.InterestRate / 100 / 12) * d.Balance;
                let minPay = d.MinimumPayment;
                if (minPay <= 0) {
                    minPay = monthlyInterest + (d.Balance * 0.01);
                }
                return {
                    ...d,
                    InitialBalance: d.Balance,
                    FixedMinPay: minPay
                };
            });
            currentDebts.sort((a, b) => b.InterestRate - a.InterestRate);
            
            let extraPayment = parseFloat(extraPaymentInput.value) || 0;
            let expectedReturn = parseFloat(expectedReturnInput.value) || 8;
            let totalMonthlyBudget = extraPayment + currentDebts.reduce((sum, d) => sum + d.FixedMinPay, 0);
            
            let totalMonths = 0;
            let netWealthHistory = [];
            let totalInvestments = 0;
            
            let month0Debt = currentDebts.reduce((sum, d) => sum + d.Balance, 0);
            netWealthHistory.push(totalInvestments - month0Debt);

            const MAX_MONTHS = 180; // Zoom in to 15 years to make divergence visible

            while (totalMonths < MAX_MONTHS) {
                totalMonths++;
                let availableBudget = totalMonthlyBudget;
                
                totalInvestments += totalInvestments * (expectedReturn / 100 / 12);

                for (let i = 0; i < currentDebts.length; i++) {
                    let debt = currentDebts[i];
                    if (debt.Balance > 0) {
                        let monthlyInterest = (debt.InterestRate / 100 / 12) * debt.Balance;
                        debt.Balance += monthlyInterest;
                        
                        let minPayToApply = Math.min(debt.FixedMinPay, debt.Balance);
                        debt.Balance -= minPayToApply;
                        availableBudget -= minPayToApply;
                    }
                }

                let monthlyExtra = availableBudget;
                let toInvest = 0;
                
                if (strategy === 'investOnly') {
                    toInvest = monthlyExtra;
                    monthlyExtra = 0;
                } else if (strategy === 'debtOnly') {
                    // Do nothing, money goes to debt
                } else if (strategy === 'intelligent') {
                    let highestRate = 0;
                    let activeDebtName = "";
                    for (let d of currentDebts) {
                        if (d.Balance > 0 && d.InterestRate > highestRate) {
                            highestRate = d.InterestRate;
                            activeDebtName = d.Name;
                        }
                    }

                    if (highestRate > expectedReturn) {
                        if (totalMonths === 1 && extraPayment > 0) {
                            optimizerCallout.classList.remove('d-none');
                            optimizerMessage.innerHTML = `Since your <strong>${activeDebtName}</strong> has an interest rate of <strong>${highestRate}%</strong> (which is higher than your expected market return of ${expectedReturn}%), you should allocate 100% of your extra budget ($${extraPayment}) toward paying it off first!`;
                        }
                    } else {
                        toInvest = monthlyExtra;
                        monthlyExtra = 0;
                        if (totalMonths === 1 && extraPayment > 0) {
                            optimizerCallout.classList.remove('d-none');
                            optimizerMessage.innerHTML = `Since your expected market return of <strong>${expectedReturn}%</strong> is higher than all your remaining debt interest rates, you should allocate 100% of your extra budget ($${extraPayment}) toward investments to maximize your net worth!`;
                        }
                    }
                }

                if (monthlyExtra > 0) {
                    for (let i = 0; i < currentDebts.length; i++) {
                        let debt = currentDebts[i];
                        if (debt.Balance > 0) {
                            let extraToApply = Math.min(debt.Balance, monthlyExtra);
                            debt.Balance -= extraToApply;
                            monthlyExtra -= extraToApply;
                            if (monthlyExtra <= 0) break;
                        }
                    }
                }

                if (monthlyExtra > 0) {
                    toInvest += monthlyExtra;
                }

                totalInvestments += toInvest;

                let monthDebt = currentDebts.reduce((sum, d) => sum + d.Balance, 0);
                netWealthHistory.push(totalInvestments - monthDebt);
            }
            
            return {
                history: netWealthHistory
            };
        };

        let payoffChart = null;

        const updateUI = () => {
            if (debts.length === 0) return;
            const snowballResult = simulate('snowball');
            const avalancheResult = simulate('avalanche');

            document.getElementById('snowballMonths').textContent = snowballResult.months;
            document.getElementById('snowballInterest').textContent = formatCurrency(snowballResult.interest);

            document.getElementById('avalancheMonths').textContent = avalancheResult.months;
            document.getElementById('avalancheInterest').textContent = formatCurrency(avalancheResult.interest);

            // Hide optimizer callout if no extra payment
            let extraPayment = parseFloat(extraPaymentInput.value) || 0;
            if (extraPayment <= 0) {
                optimizerCallout.classList.add('d-none');
            }

            const wealthDebtOnly = simulateWealth('debtOnly');
            const wealthInvestOnly = simulateWealth('investOnly');
            const wealthIntelligent = simulateWealth('intelligent');

            // Update Chart
            const ctx = document.getElementById('debtPayoffChart');
            if (ctx) {
                const maxMonths = 180; 
                const labels = Array.from({length: maxMonths}, (_, i) => `Month ${i}`);

                if (payoffChart) {
                    payoffChart.destroy();
                }

                payoffChart = new Chart(ctx, {
                    type: 'line',
                    data: {
                        labels: labels,
                        datasets: [
                            {
                                label: '100% Debt Payoff',
                                data: wealthDebtOnly.history,
                                borderColor: '#f43f5e',
                                borderWidth: 2,
                                tension: 0.1,
                                pointRadius: 0
                            },
                            {
                                label: '100% Invest',
                                data: wealthInvestOnly.history,
                                borderColor: '#0ea5e9',
                                borderWidth: 2,
                                tension: 0.1,
                                pointRadius: 0
                            },
                            {
                                label: 'Intelligent Optimizer',
                                data: wealthIntelligent.history,
                                borderColor: '#10b981',
                                borderDash: [10, 5], // Dashed so solid lines underneath are visible when overlapping
                                borderWidth: 3,
                                backgroundColor: 'rgba(16, 185, 129, 0.1)',
                                fill: true,
                                tension: 0.1,
                                pointRadius: 0
                            }
                        ]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        interaction: {
                            mode: 'index',
                            intersect: false,
                        },
                        scales: {
                            y: {
                                ticks: { callback: value => '₹' + value.toLocaleString('en-IN') }
                            },
                            x: {
                                ticks: { maxTicksLimit: 12 }
                            }
                        }
                    }
                });
            }
        };

        extraPaymentInput.addEventListener('input', updateUI);
        expectedReturnInput.addEventListener('input', updateUI);
        // Initial run
        updateUI();
    }
});
