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
      speed: 50,
    };
    this.devices = [
      {
        id: 'keyboard',
        name: 'ROG Strix Keyboard',
        model: 'ROG STRIX FLARE II',
        type: 'keyboard',
        connected: true,
        firmware: 'v1.2.3',
        battery: 85,
      },
      {
        id: 'mouse',
        name: 'ROG Gladius Mouse',
        model: 'ROG GLADIUS III',
        type: 'mouse',
        connected: true,
        firmware: 'v2.1.0',
        battery: 92,
      },
      {
        id: 'headset',
        name: 'ROG Theta Headset',
        model: 'ROG THETA 7.1',
        type: 'headset',
        connected: true,
        firmware: 'v1.5.2',
        battery: 67,
      },
    ];
    this.notificationCount = 0;

    this.init();
  }

  init() {
    this.setupEventListeners();
    this.updateUI();
    this.startMonitoring();
    this.setupKeyboardShortcuts();
    this.setupTouchSupport();
  }

  setupEventListeners() {
    // Navigation
    document.querySelectorAll('.nav-item').forEach((item) => {
      item.addEventListener('click', (e) => {
        const section = e.currentTarget.dataset.section;
        this.navigateToSection(section);
      });
    });

    // Performance Mode Cards
    document.querySelectorAll('.mode-card').forEach((card) => {
      card.addEventListener('click', (e) => {
        const mode = e.currentTarget.dataset.mode;
        this.setPerformanceMode(mode);
      });
    });

    // Lighting Controls
    const zoneSelect = document.getElementById('zone-select');
    if (zoneSelect) {
      zoneSelect.addEventListener('change', (e) => {
        this.lightingSettings.zone = e.target.value;
        this.applyLightingSettings();
      });
    }

    document.querySelectorAll('.effect-card').forEach((card) => {
      card.addEventListener('click', (e) => {
        const effect = e.currentTarget.dataset.effect;
        this.setLightingEffect(effect);
      });
    });

    const colorPicker = document.getElementById('color-picker');
    if (colorPicker) {
      colorPicker.addEventListener('change', (e) => {
        this.lightingSettings.color = e.target.value;
        this.applyLightingSettings();
      });
    }

    document.querySelectorAll('.color-preset').forEach((preset) => {
      preset.addEventListener('click', (e) => {
        const color = e.currentTarget.dataset.color;
        this.lightingSettings.color = color;
        const picker = document.getElementById('color-picker');
        if (picker) {
          picker.value = color;
        }
        this.applyLightingSettings();
      });
    });

    const brightnessSlider = document.getElementById('brightness-slider');
    if (brightnessSlider) {
      brightnessSlider.addEventListener('input', (e) => {
        this.lightingSettings.brightness = Number(e.target.value);
        const valueDisplay = e.target.nextElementSibling;
        if (valueDisplay) {
          valueDisplay.textContent = `${e.target.value}%`;
        }
        this.applyLightingSettings();
      });
    }

    const speedSlider = document.getElementById('speed-slider');
    if (speedSlider) {
      speedSlider.addEventListener('input', (e) => {
        this.lightingSettings.speed = Number(e.target.value);
        const valueDisplay = e.target.nextElementSibling;
        if (valueDisplay) {
          valueDisplay.textContent = `${e.target.value}%`;
        }
        this.applyLightingSettings();
      });
    }

    // Device Action Buttons
    document.querySelectorAll('.device-actions .btn').forEach((btn) => {
      btn.addEventListener('click', (e) => {
        const action = e.target.textContent.trim();
        const deviceCard = e.target.closest('.device-card');
        if (deviceCard) {
          const heading = deviceCard.querySelector('h4');
          if (heading) {
            this.handleDeviceAction(heading.textContent, action);
          }
        }
      });
    });
  }

  setupKeyboardShortcuts() {
    document.addEventListener('keydown', (e) => {
      if (e.ctrlKey || e.metaKey) {
        const sectionMap = { '1': 'dashboard', '2': 'system', '3': 'lighting', '4': 'devices' };
        const section = sectionMap[e.key];
        if (section) {
          e.preventDefault();
          this.navigateToSection(section);
        }
      }
    });
  }

  setupTouchSupport() {
    let touchStartX = 0;

    document.addEventListener('touchstart', (e) => {
      touchStartX = e.changedTouches[0].screenX;
    });

    document.addEventListener('touchend', (e) => {
      const touchEndX = e.changedTouches[0].screenX;
      const swipeThreshold = 50;
      const diff = touchStartX - touchEndX;

      if (Math.abs(diff) > swipeThreshold) {
        const sections = ['dashboard', 'system', 'lighting', 'devices'];
        const currentIndex = sections.indexOf(this.currentSection);

        if (diff > 0 && currentIndex < sections.length - 1) {
          this.navigateToSection(sections[currentIndex + 1]);
        } else if (diff < 0 && currentIndex > 0) {
          this.navigateToSection(sections[currentIndex - 1]);
        }
      }
    });
  }

  navigateToSection(section) {
    // Update navigation
    document.querySelectorAll('.nav-item').forEach((item) => {
      item.classList.remove('active');
    });
    const navTarget = document.querySelector(`[data-section="${section}"]`);
    if (navTarget) {
      navTarget.classList.add('active');
    }

    // Update content
    document.querySelectorAll('.section').forEach((sec) => {
      sec.classList.remove('active');
    });
    const sectionEl = document.getElementById(`${section}-section`);
    if (sectionEl) {
      sectionEl.classList.add('active');
    }

    // Update page title
    const titles = {
      dashboard: 'Dashboard',
      system: 'System',
      lighting: 'Lighting',
      devices: 'Devices',
    };
    const pageTitle = document.getElementById('page-title');
    if (pageTitle) {
      pageTitle.textContent = titles[section] || section;
    }

    this.currentSection = section;
  }

  setPerformanceMode(mode) {
    this.performanceMode = mode;

    // Update UI
    document.querySelectorAll('.mode-card').forEach((card) => {
      card.classList.remove('active');
    });
    const modeCard = document.querySelector(`[data-mode="${mode}"]`);
    if (modeCard) {
      modeCard.classList.add('active');
    }

    // Show notification
    this.showNotification(
      `Performance mode set to ${mode.charAt(0).toUpperCase() + mode.slice(1)}`
    );

    // Simulate system changes
    this.updateSystemMonitoring(mode);
  }

  setLightingEffect(effect) {
    this.lightingSettings.effect = effect;

    // Update UI
    document.querySelectorAll('.effect-card').forEach((card) => {
      card.classList.remove('active');
    });
    const effectCard = document.querySelector(`[data-effect="${effect}"]`);
    if (effectCard) {
      effectCard.classList.add('active');
    }

    this.applyLightingSettings();
  }

  applyLightingSettings() {
    this.showNotification('Lighting settings applied');
  }

  handleDeviceAction(deviceName, action) {
    const device = this.devices.find((d) => d.name === deviceName);
    const displayName = device ? device.name : deviceName;

    if (action === 'Settings') {
      this.showNotification(`Opening settings for ${displayName}`);
    } else if (action === 'Update') {
      this.showNotification(`Checking for updates for ${displayName}`);
    }
  }

  updateSystemMonitoring(mode) {
    const modeSettings = {
      performance: { cpu: 85, memory: 70, gpu: 95, temp: 82 },
      balanced: { cpu: 60, memory: 45, gpu: 80, temp: 75 },
      quiet: { cpu: 35, memory: 30, gpu: 50, temp: 65 },
    };

    const settings = modeSettings[mode];
    if (!settings) {
      return;
    }

    // Update monitoring bars with animation
    setTimeout(() => {
      const fills = document.querySelectorAll('.monitor-fill');
      const values = document.querySelectorAll('.monitor-value');

      if (fills.length >= 4 && values.length >= 4) {
        fills[0].style.width = `${settings.cpu}%`;
        values[0].textContent = `${settings.cpu}%`;

        fills[1].style.width = `${settings.memory}%`;
        values[1].textContent = `${settings.memory}%`;

        fills[2].style.width = `${settings.gpu}%`;
        values[2].textContent = `${settings.gpu}%`;

        fills[3].style.width = `${settings.temp}%`;
        values[3].textContent = `${settings.temp}\u00B0C`;
      }
    }, 300);
  }

  startMonitoring() {
    // Simulate real-time monitoring updates
    this._monitoringInterval = setInterval(() => {
      this.updateMonitoringData();
    }, 5000);
  }

  stopMonitoring() {
    if (this._monitoringInterval) {
      clearInterval(this._monitoringInterval);
      this._monitoringInterval = null;
    }
  }

  updateMonitoringData() {
    // Add small random variations to monitoring data
    const fills = document.querySelectorAll('.monitor-fill');
    const values = document.querySelectorAll('.monitor-value');

    fills.forEach((fill, index) => {
      if (index < 3) {
        // CPU, Memory, GPU
        const currentWidth = parseInt(fill.style.width, 10) || 50;
        const variation = Math.floor(Math.random() * 10) - 5;
        const newWidth = Math.max(10, Math.min(95, currentWidth + variation));
        fill.style.width = `${newWidth}%`;
        if (values[index]) {
          values[index].textContent = `${newWidth}%`;
        }
      }
    });
  }

  updateUI() {
    // Set initial performance mode
    this.setPerformanceMode(this.performanceMode);

    // Set initial lighting effect
    this.setLightingEffect(this.lightingSettings.effect);

    // Set initial values for sliders
    const brightnessSlider = document.getElementById('brightness-slider');
    const speedSlider = document.getElementById('speed-slider');
    const colorPicker = document.getElementById('color-picker');

    if (brightnessSlider) {
      brightnessSlider.value = this.lightingSettings.brightness;
    }
    if (speedSlider) {
      speedSlider.value = this.lightingSettings.speed;
    }
    if (colorPicker) {
      colorPicker.value = this.lightingSettings.color;
    }

    // Update slider value displays
    const brightnessValue = document.querySelector('#brightness-slider + .slider-value');
    if (brightnessValue) {
      brightnessValue.textContent = `${this.lightingSettings.brightness}%`;
    }

    const speedValue = document.querySelector('#speed-slider + .slider-value');
    if (speedValue) {
      speedValue.textContent = `${this.lightingSettings.speed}%`;
    }
  }

  showNotification(message) {
    // Stack notifications so they don't overlap
    this.notificationCount += 1;
    const offset = 20 + (this.notificationCount - 1) * 52;

    const notification = document.createElement('div');
    notification.className = 'notification';
    notification.textContent = message;
    notification.style.cssText = `
      position: fixed;
      top: ${offset}px;
      right: 20px;
      background: var(--accent-gradient);
      color: white;
      padding: 12px 20px;
      border-radius: 8px;
      box-shadow: var(--shadow-lg);
      z-index: 10000;
      animation: slideIn 0.3s ease;
    `;

    document.body.appendChild(notification);

    setTimeout(() => {
      notification.style.animation = 'slideOut 0.3s ease';
      setTimeout(() => {
        if (notification.parentNode) {
          notification.parentNode.removeChild(notification);
        }
        this.notificationCount = Math.max(0, this.notificationCount - 1);
      }, 300);
    }, 3000);
  }
}

// Add notification animations to CSS
const notificationStyle = document.createElement('style');
notificationStyle.textContent = `
  @keyframes slideIn {
    from { transform: translateX(100%); opacity: 0; }
    to { transform: translateX(0); opacity: 1; }
  }

  @keyframes slideOut {
    from { transform: translateX(0); opacity: 1; }
    to { transform: translateX(100%); opacity: 0; }
  }
`;
document.head.appendChild(notificationStyle);

// Initialize app when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
  window.app = new ArmouryCrateApp();
});

// Export for potential external use
if (typeof module !== 'undefined' && module.exports) {
  module.exports = ArmouryCrateApp;
}
