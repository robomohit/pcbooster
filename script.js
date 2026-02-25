// Armoury Crate Lite JavaScript
class ArmouryCrateApp {
    constructor() {
        this.currentSection = 'dashboard';
        this.performanceMode = 'balanced';
        this.lightingSettings = {
            zone: 'all',
            effect: 'static',
            color: '#0066ff',
            brightness: 80,
            speed: 50
        };
        this.devices = [
            {
                id: 'keyboard',
                name: 'ROG Strix Keyboard',
                model: 'ROG STRIX FLARE II',
                type: 'keyboard',
                connected: true,
                firmware: 'v1.2.3',
                battery: 85
            },
            {
                id: 'mouse',
                name: 'ROG Gladius Mouse',
                model: 'ROG GLADIUS III',
                type: 'mouse',
                connected: true,
                firmware: 'v2.1.0',
                battery: 92
            },
            {
                id: 'headset',
                name: 'ROG Theta Headset',
                model: 'ROG THETA 7.1',
                type: 'headset',
                connected: true,
                firmware: 'v1.5.2',
                battery: 67
            }
        ];
        
        this.init();
    }

    init() {
        this.setupEventListeners();
        this.updateUI();
        this.startMonitoring();
    }

    setupEventListeners() {
        // Navigation
        document.querySelectorAll('.nav-item').forEach(item => {
            item.addEventListener('click', (e) => {
                const section = e.currentTarget.dataset.section;
                this.navigateToSection(section);
            });
        });

        // Performance Mode Cards
        document.querySelectorAll('.mode-card').forEach(card => {
            card.addEventListener('click', (e) => {
                const mode = e.currentTarget.dataset.mode;
                this.setPerformanceMode(mode);
            });
        });

        // Lighting Controls
        document.getElementById('zone-select').addEventListener('change', (e) => {
            this.lightingSettings.zone = e.target.value;
            this.applyLightingSettings();
        });

        document.querySelectorAll('.effect-card').forEach(card => {
            card.addEventListener('click', (e) => {
                const effect = e.currentTarget.dataset.effect;
                this.setLightingEffect(effect);
            });
        });

        document.getElementById('color-picker').addEventListener('change', (e) => {
            this.lightingSettings.color = e.target.value;
            this.applyLightingSettings();
        });

        document.querySelectorAll('.color-preset').forEach(preset => {
            preset.addEventListener('click', (e) => {
                const color = e.currentTarget.dataset.color;
                this.lightingSettings.color = color;
                document.getElementById('color-picker').value = color;
                this.applyLightingSettings();
            });
        });

        document.getElementById('brightness-slider').addEventListener('input', (e) => {
            this.lightingSettings.brightness = e.target.value;
            e.target.nextElementSibling.textContent = `${e.target.value}%`;
            this.applyLightingSettings();
        });

        document.getElementById('speed-slider').addEventListener('input', (e) => {
            this.lightingSettings.speed = e.target.value;
            e.target.nextElementSibling.textContent = `${e.target.value}%`;
            this.applyLightingSettings();
        });

        // Device Action Buttons
        document.querySelectorAll('.device-actions .btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const action = e.target.textContent.trim();
                const deviceCard = e.target.closest('.device-card');
                const deviceName = deviceCard.querySelector('h4').textContent;
                this.handleDeviceAction(deviceName, action);
            });
        });
    }

    navigateToSection(section) {
        // Update navigation
        document.querySelectorAll('.nav-item').forEach(item => {
            item.classList.remove('active');
        });
        document.querySelector(`[data-section="${section}"]`).classList.add('active');

        // Update content
        document.querySelectorAll('.section').forEach(sec => {
            sec.classList.remove('active');
        });
        document.getElementById(`${section}-section`).classList.add('active');

        // Update page title
        const titles = {
            dashboard: 'Dashboard',
            system: 'System',
            lighting: 'Lighting',
            devices: 'Devices'
        };
        document.getElementById('page-title').textContent = titles[section];

        this.currentSection = section;
    }

    setPerformanceMode(mode) {
        this.performanceMode = mode;
        
        // Update UI
        document.querySelectorAll('.mode-card').forEach(card => {
            card.classList.remove('active');
        });
        document.querySelector(`[data-mode="${mode}"]`).classList.add('active');

        // Show notification
        this.showNotification(`Performance mode set to ${mode.charAt(0).toUpperCase() + mode.slice(1)}`);

        // Simulate system changes
        this.updateSystemMonitoring(mode);
    }

    setLightingEffect(effect) {
        this.lightingSettings.effect = effect;
        
        // Update UI
        document.querySelectorAll('.effect-card').forEach(card => {
            card.classList.remove('active');
        });
        document.querySelector(`[data-effect="${effect}"]`).classList.add('active');

        this.applyLightingSettings();
    }

    applyLightingSettings() {
        // Simulate applying lighting settings
        console.log('Applying lighting settings:', this.lightingSettings);
        this.showNotification('Lighting settings applied');
    }

    handleDeviceAction(deviceName, action) {
        if (action === 'Settings') {
            this.showNotification(`Opening settings for ${deviceName}`);
        } else if (action === 'Update') {
            this.showNotification(`Checking for updates for ${deviceName}`);
        }
    }

    updateSystemMonitoring(mode) {
        const modeSettings = {
            performance: { cpu: 85, memory: 70, gpu: 95, temp: 82 },
            balanced: { cpu: 60, memory: 45, gpu: 80, temp: 75 },
            quiet: { cpu: 35, memory: 30, gpu: 50, temp: 65 }
        };

        const settings = modeSettings[mode];
        
        // Update monitoring bars with animation
        setTimeout(() => {
            const fills = document.querySelectorAll('.monitor-fill');
            const values = document.querySelectorAll('.monitor-value');
            
            fills[0].style.width = `${settings.cpu}%`;
            values[0].textContent = `${settings.cpu}%`;
            
            fills[1].style.width = `${settings.memory}%`;
            values[1].textContent = `${settings.memory}%`;
            
            fills[2].style.width = `${settings.gpu}%`;
            values[2].textContent = `${settings.gpu}%`;
            
            fills[3].style.width = `${(settings.temp / 100) * 100}%`;
            values[3].textContent = `${settings.temp}°C`;
        }, 300);
    }

    startMonitoring() {
        // Simulate real-time monitoring updates
        setInterval(() => {
            this.updateMonitoringData();
        }, 5000);
    }

    updateMonitoringData() {
        // Add small random variations to monitoring data
        const fills = document.querySelectorAll('.monitor-fill');
        const values = document.querySelectorAll('.monitor-value');
        
        fills.forEach((fill, index) => {
            if (index < 3) { // CPU, Memory, GPU
                const currentWidth = parseInt(fill.style.width);
                const variation = Math.floor(Math.random() * 10) - 5;
                const newWidth = Math.max(10, Math.min(95, currentWidth + variation));
                fill.style.width = `${newWidth}%`;
                values[index].textContent = `${newWidth}%`;
            }
        });
    }

    updateUI() {
        // Set initial performance mode
        this.setPerformanceMode(this.performanceMode);
        
        // Set initial lighting effect
        this.setLightingEffect(this.lightingSettings.effect);
        
        // Set initial values for sliders
        document.getElementById('brightness-slider').value = this.lightingSettings.brightness;
        document.getElementById('speed-slider').value = this.lightingSettings.speed;
        document.getElementById('color-picker').value = this.lightingSettings.color;
        
        // Update slider value displays
        document.querySelectorAll('#brightness-slider + .slider-value')[0].textContent = `${this.lightingSettings.brightness}%`;
        document.querySelectorAll('#speed-slider + .slider-value')[0].textContent = `${this.lightingSettings.speed}%`;
    }

    showNotification(message) {
        // Create notification element
        const notification = document.createElement('div');
        notification.className = 'notification';
        notification.textContent = message;
        notification.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: var(--accent-gradient);
            color: white;
            padding: 12px 20px;
            border-radius: 8px;
            box-shadow: var(--shadow-lg);
            z-index: 10000;
            animation: slideIn 0.3s ease;
        `;

        // Add to DOM
        document.body.appendChild(notification);

        // Remove after 3 seconds
        setTimeout(() => {
            notification.style.animation = 'slideOut 0.3s ease';
            setTimeout(() => {
                document.body.removeChild(notification);
            }, 300);
        }, 3000);
    }
}

// Add notification animations to CSS
const style = document.createElement('style');
style.textContent = `
    @keyframes slideIn {
        from { transform: translateX(100%); opacity: 0; }
        to { transform: translateX(0); opacity: 1; }
    }
    
    @keyframes slideOut {
        from { transform: translateX(0); opacity: 1; }
        to { transform: translateX(100%); opacity: 0; }
    }
`;
document.head.appendChild(style);

// Initialize app when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.app = new ArmouryCrateApp();
});

// Add keyboard shortcuts
document.addEventListener('keydown', (e) => {
    if (e.ctrlKey || e.metaKey) {
        switch(e.key) {
            case '1':
                e.preventDefault();
                window.app.navigateToSection('dashboard');
                break;
            case '2':
                e.preventDefault();
                window.app.navigateToSection('system');
                break;
            case '3':
                e.preventDefault();
                window.app.navigateToSection('lighting');
                break;
            case '4':
                e.preventDefault();
                window.app.navigateToSection('devices');
                break;
        }
    }
});

// Add touch support for mobile
let touchStartX = 0;
let touchEndX = 0;

document.addEventListener('touchstart', (e) => {
    touchStartX = e.changedTouches[0].screenX;
});

document.addEventListener('touchend', (e) => {
    touchEndX = e.changedTouches[0].screenX;
    handleSwipe();
});

function handleSwipe() {
    const swipeThreshold = 50;
    const diff = touchStartX - touchEndX;
    
    if (Math.abs(diff) > swipeThreshold) {
        const sections = ['dashboard', 'system', 'lighting', 'devices'];
        const currentIndex = sections.indexOf(window.app.currentSection);
        
        if (diff > 0 && currentIndex < sections.length - 1) {
            // Swipe left - next section
            window.app.navigateToSection(sections[currentIndex + 1]);
        } else if (diff < 0 && currentIndex > 0) {
            // Swipe right - previous section
            window.app.navigateToSection(sections[currentIndex - 1]);
        }
    }
}

// Export for potential external use
if (typeof module !== 'undefined' && module.exports) {
    module.exports = ArmouryCrateApp;
}
