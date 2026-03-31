let ws = null;
let isSearching = false;
let foundSeeds = [];
let currentSearchUseLegacy = false;
let seedDetailsCache = {};
let nextStartSeed = 0;
let currentStartSeedDisplay;
let currentEndSeedDisplay;

let nextFairyIndex = 0;

let mineChestConditions = [];
let nextMineChestIndex = 1;

let monsterLevelConditions = [];
let nextMonsterLevelIndex = 1;

let ALL_CART_ITEM_NAMES = [];

const elements = {
    form: document.getElementById('searchForm'),
    searchBtn: document.getElementById('searchBtn'),
    progressSection: document.getElementById('progressSection'),
    resultsSection: document.getElementById('resultsSection'),
    progressBar: document.getElementById('progressBar'),
    statusMessage: document.getElementById('statusMessage'),
    checkedCount: document.getElementById('checkedCount'),
    foundCount: document.getElementById('foundCount'),
    speed: document.getElementById('speed'),
    elapsed: document.getElementById('elapsed'),
    eta: document.getElementById('eta'), //预估时长
    seedList: document.getElementById('seedList'),
    resultsSummary: document.getElementById('resultsSummary'),
    connectionStatus: document.getElementById('connectionStatus'),
    weatherEnabled: document.getElementById('weatherEnabled'),
    weatherConfig: document.getElementById('weatherConfig'),
    conditionsList: document.getElementById('conditionsList'),
    conditionError: document.getElementById('conditionError'),

    fairyEnabled: document.getElementById('fairyEnabled'),
    fairyConfig: document.getElementById('fairyConfig'),
    fairyConditionError: document.getElementById('fairyConditionError'),

    mineChestEnabled: document.getElementById('mineChestEnabled'),
    mineChestConfig: document.getElementById('mineChestConfig'),
    mineChestConditionError: document.getElementById('mineChestConditionError'),

    monsterLevelEnabled: document.getElementById('monsterLevelEnabled'),
    monsterLevelConfig: document.getElementById('monsterLevelConfig'),
    monsterLevelConditionError: document.getElementById('monsterLevelConditionError'),

    desertFestivalEnabled: document.getElementById('desertFestivalEnabled'),
    desertFestivalConfig: document.getElementById('desertFestivalConfig'),
    requireJas: document.getElementById('requireJas'),
    requireLeah: document.getElementById('requireLeah'),

    cartSection: document.getElementById('cartSection'),
    sidebarCartContent: document.getElementById('sidebarCartContent'),
    cartEnabled: document.getElementById('cartEnabled'),
    cartConfig: document.getElementById('cartConfig'),
    cartConditionsContainer: document.getElementById('cartConditionsContainer'),
    cartConditionError: document.getElementById('cartConditionError')
};

// 混合宝箱数据
const MINE_CHEST_ITEMS = {
    10: ["皮靴", "工作靴", "木剑", "铁制短剑", "疾风利剑", "股骨"],
    20: ["钢制轻剑", "木棒", "精灵之刃", "光辉戒指", "磁铁戒指"],
    50: ["冻土靴", "热能靴", "战靴", "镀银军刀", "海盗剑"],
    60: ["水晶匕首", "弯刀", "铁刃", "飞贼之胫", "木锤"],
    80: ["蹈火者靴", "黑暗之靴", "双刃大剑", "圣堂之刃", "长柄锤", "暗影匕首"],
    90: ["黑曜石之刃", "淬火阔剑", "蛇形邪剑", "骨剑", "骨化剑"],
    110: ["太空之靴", "水晶鞋", "钢刀", "巨锤"]
};
const ALL_MINE_FLOORS = [10, 20, 50, 60, 80, 90, 110];

// 日期转换
const DaysPerSeason = 28;
const SeasonsPerYear = 4;
const DaysPerYear = DaysPerSeason * SeasonsPerYear;
const SeasonNames = ["春", "夏", "秋", "冬"];
const SeasonNameToIndex = { '春': 0, '夏': 1, '秋': 2, '冬': 3 };

/**
 * 转换为绝对天数（从 第1年春季第1天 = 0 开始）
 * @param {number} year - 年份，从1开始
 * @param {number} season - 季节（0=春季，1=夏季，2=秋季，3=冬季）
 * @param {number} day - 日期（1-28）
 * @returns {number} 绝对天数
 */
function dateToAbsoluteDay(year, season, day) {
    // year starts from 1
    const yearOffset = (year - 1) * DaysPerYear;
    const seasonOffset = season * DaysPerSeason;
    const dayOffset = day;

    return yearOffset + seasonOffset + dayOffset;
}

//这是UI用的日期转换，区分一下
function formatTime(seconds) {
    if (!isFinite(seconds) || seconds < 0) return '--';
    if (seconds < 60) return seconds.toFixed(1) + 's';
    const d = Math.floor(seconds / 86400);
    const h = Math.floor((seconds % 86400) / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = Math.floor(seconds % 60);
    let result = [];
    if (d > 0) result.push(`${d}d`);
    if (h > 0) result.push(`${h}h`);
    if (m > 0) result.push(`${m}m`);
    if (s > 0 || result.length === 0) result.push(`${s}s`);
    return result.join(' ');
}

/**
 * 从绝对天数还原为 (年, 季节, 日)
 * @param {number} absoluteDay - 绝对天数
 * @returns {Object} 包含年、季节和日的对象
 */
function absoluteDayToDate(absoluteDay) {
    let dayOfYear = absoluteDay % DaysPerYear;
    if (dayOfYear === 0) {
        dayOfYear = DaysPerYear;
    }

    const year = Math.floor((absoluteDay - dayOfYear) / DaysPerYear) + 1;

    let day = dayOfYear % DaysPerSeason;
    if (day === 0) {
        day = DaysPerSeason;
    }

    const season = Math.floor((dayOfYear - day) / DaysPerSeason);

    return { year, season, day };
}


// 天气
elements.weatherEnabled.addEventListener('change', (e) => {
    elements.weatherConfig.style.display = e.target.checked ? 'block' : 'none';
});
// 仙子
elements.fairyEnabled.addEventListener('change', (e) => {
    elements.fairyConfig.style.display = e.target.checked ? 'block' : 'none';
});
// 混合矿井宝箱
elements.mineChestEnabled.addEventListener('change', (e) => {
    elements.mineChestConfig.style.display = e.target.checked ? 'block' : 'none';
});
// 怪物层
elements.monsterLevelEnabled.addEventListener('change', (e) => {
    elements.monsterLevelConfig.style.display = e.target.checked ? 'block' : 'none';
});
// 沙漠节
elements.desertFestivalEnabled.addEventListener('change', (e) => {
    elements.desertFestivalConfig.style.display = e.target.checked ? 'block' : 'none';
});

// 猪车
elements.cartEnabled.addEventListener('change', (e) => {
    elements.cartConfig.style.display = e.target.checked ? 'block' : 'none';
});

// 添加天气条件
function addWeatherCondition() {
    const container = document.getElementById('conditionsContainer');
    const template = document.getElementById('weatherConditionTemplate');

    const clone = template.content.cloneNode(true);
    const row = clone.querySelector('.weather-condition-row');

    // 删除逻辑：点击时直接移除 DOM 元素
    row.querySelector('.btn-remove').onclick = () => {
        row.remove();
    };

    container.appendChild(clone);
    hideError(); // 添加时尝试清理错误提示
}

// 同步条件数据（从DOM读取）
function syncConditions() {
    const rows = document.querySelectorAll('.weather-condition-row');
    conditions = Array.from(rows).map(row => {
        const inputs = row.querySelectorAll('select, input');
        return {
            season: inputs[0].value,
            startDay: parseInt(inputs[1].value) || 1,
            endDay: parseInt(inputs[2].value) || 28,
            minRain: parseInt(inputs[3].value) || 1
        };
    });
}

// 验证单个条件
function validateWeatherCondition(condition) {
    const { startDay, endDay, minRainDays } = condition;

    if (startDay > endDay) {
        return { valid: false, error: '起始日期不能大于结束日期' };
    }

    const dayCount = endDay - startDay + 1;
    if (minRainDays < 1 || minRainDays > dayCount) {
        return { valid: false, error: `要求雨天数(${minRainDays})不能超过范围总天数(${dayCount})` };
    }

    return { valid: true };
}

// 检查天气重叠 (基于绝对天数)
function hasWeatherOverlap(newCond, allConfigs) {
    // 计算当前条件的绝对范围 (第一年)
    const newStart = dateToAbsoluteDay(1, newCond.season, newCond.startDay);
    const newEnd = dateToAbsoluteDay(1, newCond.season, newCond.endDay);

    return allConfigs.some(config => {
        const start = dateToAbsoluteDay(1, config.season, config.startDay);
        const end = dateToAbsoluteDay(1, config.season, config.endDay);
        // 判断两个区间是否有交集
        return (newStart <= end && newEnd >= start);
    });
}

// 显示错误
function showError(message) {
    const errorDiv = document.getElementById('conditionError');
    errorDiv.textContent = message;
    errorDiv.classList.add('show');
    errorDiv.style.display = 'block';
}

// 隐藏错误
function hideError() {
    const errorDiv = document.getElementById('conditionError');
    if (errorDiv) {
        errorDiv.textContent = '';
        errorDiv.classList.remove('show');
        errorDiv.style.display = 'none';
    }
}

function addFairyCondition() {
    const container = document.getElementById('fairyConditionsContainer');
    const template = document.getElementById('fairyConditionTemplate');

    // 克隆模板
    const clone = template.content.cloneNode(true);
    const row = clone.querySelector('.fairy-condition-row');

    // 删除条件
    row.querySelector('.btn-remove').onclick = () => {
        row.remove();
    };

    container.appendChild(clone);
}

function validateFairyCondition(condition) {
    const { startYear, startSeason, startDay, endYear, endSeason, endDay } = condition;

    // 绝对天数验证逻辑 (1年112天, 1季28天)
    const startAbs = dateToAbsoluteDay(startYear, startSeason, startDay);
    const endAbs = dateToAbsoluteDay(endYear, endSeason, endDay);

    if (startAbs > endAbs) {
        return { valid: false, error: '仙子搜索结束日期不能早于开始日期' };
    }

    return { valid: true };
}

function isFairyDuplicate(currentCondition, allConditions) {
    return allConditions.some(c =>
        c.startYear === currentCondition.startYear &&
        c.startSeason === currentCondition.startSeason &&
        c.startDay === currentCondition.startDay &&
        c.endYear === currentCondition.endYear &&
        c.endSeason === currentCondition.endSeason &&
        c.endDay === currentCondition.endDay
    );
}

// 添加矿井宝箱条件
function addMineChestCondition(targetFloor = null, targetItem = null) {
    const container = document.getElementById('mineChestConditionsContainer');
    const template = document.getElementById('mineChestConditionTemplate');

    // 1. 获取当前页面已有的所有层数
    const existingFloors = Array.from(document.querySelectorAll('.minechest-floor'))
        .map(select => parseInt(select.value));

    let floorToSet = targetFloor;
    let itemToSet = targetItem;

    // 2. 如果没指定层数（点击“添加条件”按钮时），寻找下一个可用层数
    if (floorToSet === null) {
        const availableFloors = ALL_MINE_FLOORS.filter(f => !existingFloors.includes(f));

        if (availableFloors.length === 0) {
            alert("所有矿井层数已设置完毕，无法继续添加。");
            return;
        }
        // 自动取剩余层数里的第一个
        floorToSet = availableFloors[0];
        // 自动取该层数物品池里的第一个
        itemToSet = MINE_CHEST_ITEMS[floorToSet][0];
    }

    // 3. 实例化模板
    const clone = template.content.cloneNode(true);
    const row = clone.querySelector('.minechest-condition-row');
    const floorSelect = row.querySelector('.minechest-floor');
    const itemSelect = row.querySelector('.minechest-item');

    // 4. 初始化下拉框值
    floorSelect.value = floorToSet;
    populateMineItemOptions(floorToSet, itemSelect);
    if (itemToSet) itemSelect.value = itemToSet;

    // 5. 绑定联动逻辑
    floorSelect.onchange = () => {
        // 检查是否选择了其他行已经选过的层数（可选增加）
        populateMineItemOptions(floorSelect.value, itemSelect);
    };

    // 6. 删除逻辑
    row.querySelector('.btn-remove').onclick = () => {
        row.remove();
    };

    container.appendChild(clone);
}

// 辅助函数：根据层数填充下拉框
function populateMineItemOptions(floor, selectElement) {
    const items = MINE_CHEST_ITEMS[floor] || [];
    selectElement.innerHTML = items.map(item => `<option value="${item}">${item}</option>`).join('');
}

// 添加怪物层条件
function addMonsterLevelCondition() {
    const container = document.getElementById('monsterLevelConditionsContainer');
    const template = document.getElementById('monsterLevelConditionTemplate');

    const clone = template.content.cloneNode(true);
    const row = clone.querySelector('.monsterlevel-condition-row');

    // 删除逻辑
    row.querySelector('.btn-remove').onclick = () => {
        row.remove();
    };

    container.appendChild(clone);
}

// 检查怪物层数据
function validateMonsterLevelCondition(condition) {
    const { startSeason, startDay, endSeason, endDay, startLevel, endLevel } = condition;

    // 1. 日期验证 (利用之前定义的 dateToAbsoluteDay)
    const startAbs = dateToAbsoluteDay(1, startSeason, startDay);
    const endAbs = dateToAbsoluteDay(1, endSeason, endDay);

    if (startAbs > endAbs) {
        return { valid: false, error: '日期范围：起始日期不能晚于结束日期' };
    }

    // 2. 层数验证
    if (startLevel > endLevel) {
        return { valid: false, error: '层数范围：起始层数不能大于结束层数' };
    }

    return { valid: true };
}

// 加载所有猪车物品列表
async function loadCartItems() {
    try {
        const response = await fetch('http://localhost:5000/api/cart-items');
        ALL_CART_ITEM_NAMES = await response.json();
        initializeCartItemList(); // 更新datalist
    } catch (error) {
        console.error('加载物品列表失败:', error);
    }
}

// 添加新的猪车条件行
function addCartCondition() {
    const container = elements.cartConditionsContainer;
    const template = document.getElementById('cartConditionTemplate');

    const clone = template.content.cloneNode(true);
    const row = clone.querySelector('.cart-condition-row');

    const filterInput = row.querySelector('.cart-item-filter-input');
    const itemSelect = row.querySelector('.cart-item-select');

    filterInput.addEventListener('input', (e) => {
        const keyword = e.target.value.trim().toLowerCase();
        // 过滤全局物品列表
        const filtered = ALL_CART_ITEM_NAMES.filter(name =>
            name.toLowerCase().includes(keyword)
        );

        // 更新下拉框内容
        itemSelect.innerHTML = '<option value="">--请选择--</option>';
        filtered.forEach(name => {
            const opt = document.createElement('option');
            opt.value = name;
            opt.textContent = name;
            itemSelect.appendChild(opt);
        });

        // 如果过滤结果只有一个，自动选中它
        if (filtered.length === 1) {
            itemSelect.value = filtered[0];
        }
    });

    // “多次出现”联动逻辑
    const multiCheck = row.querySelector('.cart-multi-check');
    const multiWrap = row.querySelector('.cart-multi-count-wrap');
    const multiInput = row.querySelector('.cart-min-occurrences');

    // 初始状态（未勾选时禁用）
    multiInput.disabled = !multiCheck.checked;
    multiCheck.addEventListener('change', () => {
        // 只控制是否可编辑
        multiInput.disabled = !multiCheck.checked;
    });

    // 移除按钮
    row.querySelector('.btn-remove').onclick = () => {
        row.remove();
    };

    container.appendChild(clone);
}

// 验证猪车条件
function validateCartCondition(condition) {
    const { startYear, startSeason, startDay, endYear, endSeason, endDay, itemName } = condition;

    if (!itemName || itemName === "") {
        return { valid: false, error: '请在猪车下拉菜单中选择一个具体的物品' };
    }

    // 跨年绝对日期验证 (利用绝对天数)
    const startAbs = dateToAbsoluteDay(startYear, startSeason, startDay);
    const endAbs = dateToAbsoluteDay(endYear, endSeason, endDay);

    if (startAbs > endAbs) {
        return { valid: false, error: '猪车起始日期不能晚于结束日期' };
    }

    return { valid: true };
}

// 初始化猪车列表
function initializeCartItemList() {
    const datalist = document.getElementById('cartItemNamesList');
    if (!datalist) return;

    // 清空旧选项，防止重复堆积
    datalist.innerHTML = '';

    // 填充新选项
    ALL_CART_ITEM_NAMES.forEach(item => {
        const option = document.createElement('option');
        option.value = item;
        datalist.appendChild(option);
    });
}

// 最大输出种子数量
function updateOutputLimitMax() {
    const rangeSelect = document.getElementById('searchRange');
    const outputLimitInput = document.getElementById('outputLimit');
    const startSeedInput = document.getElementById('startSeed');

    // 当种子范围为“最大”，计算范围
    let range;
    if (rangeSelect.value === "max") {
        const startSeed = parseInt(startSeedInput.value) || 1;
        range = 2147483647 - startSeed + 1;
    } else {
        range = parseInt(rangeSelect.value);
    }

    const limit = parseInt(outputLimitInput.value) || 10; // 默认10个结果

    if (range <= limit) {
        // 如果当前值超过了新的最大值，就把它降下来
        outputLimitInput.value = range;
    }
}

document.addEventListener('DOMContentLoaded', function () {

    // 天气条件初始化
    addWeatherCondition();

    // 仙子条件初始化
    addFairyCondition();

    // 矿井宝箱条件初始化
    addMineChestCondition("110", "巨锤");

    // 怪物层条件初始化
    addMonsterLevelCondition(0);

    // 猪车条件初始化
    loadCartItems();
    initializeCartItemList(); // 初始化物品 datalist
    addCartCondition(); // 添加第一个条件行
});

// 监听起始种子修改,重置循环
document.getElementById('startSeed').addEventListener('change', function () {
    nextStartSeed = parseInt(this.value) || 0;
});

// 监听搜索范围修改,重置循环
document.getElementById('searchRange').addEventListener('change', function () {
    // 1. 重置循环逻辑：让下一次搜索从当前输入的起始种子开始
    const startSeed = parseInt(document.getElementById('startSeed').value) || 0;
    nextStartSeed = startSeed;

    // 2. 更新输出上限逻辑：确保输出种子数不会比搜索范围还大
    updateOutputLimitMax();
});

// 监听循环搜索复选框
document.getElementById('loopSearch').addEventListener('change', function () {
    if (!this.checked) {
        // 取消循环时重置
        const startSeed = parseInt(document.getElementById('startSeed').value) || 0;
        nextStartSeed = startSeed;
    }
});

// 让页面加载后，以及每次修改种子范围时，都更新这个最大值
document.addEventListener('DOMContentLoaded', updateOutputLimitMax);
document.getElementById('startSeed').addEventListener('input', updateOutputLimitMax);

function connectWebSocket() {
    elements.connectionStatus.textContent = '连接中...';
    elements.connectionStatus.className = 'connection-status connecting';

    ws = new WebSocket('ws://localhost:5000/ws');

    ws.onopen = () => {
        elements.connectionStatus.textContent = '✓ 已连接';
        elements.connectionStatus.className = 'connection-status connected';
    };

    ws.onmessage = (event) => {
        const data = JSON.parse(event.data);
        handleWebSocketMessage(data);
    };

    ws.onerror = () => {
        elements.connectionStatus.textContent = '✗ 连接失败';
        elements.connectionStatus.className = 'connection-status disconnected';
    };

    ws.onclose = () => {
        elements.connectionStatus.textContent = '✗ 未连接';
        elements.connectionStatus.className = 'connection-status disconnected';
        setTimeout(connectWebSocket, 5000);
    };
}

function handleWebSocketMessage(data) {
    switch (data.type) {
        case 'start':
            foundSeeds = [];
            elements.seedList.innerHTML = '';
            elements.resultsSection.style.display = 'block';

            document.getElementById('searchRangeBadge').textContent = `种子范围: ${currentStartSeedDisplay.toLocaleString()}-${currentEndSeedDisplay.toLocaleString()}`;
            document.getElementById('searchRangeBadge').style.display = 'inline-block';
            document.getElementById('analysisDetails').style.display = 'block';
            document.getElementById('analysisStoppedEarly').textContent = '?';
            document.getElementById('analysisStoppedEarly').style.color = '#999';

            const filterStatsList = document.getElementById('filterstatsList');
            if (filterStatsList) { filterStatsList.innerHTML = ''; }
            break;

        case 'progress':
            elements.checkedCount.textContent = data.checkedCount.toLocaleString();
            elements.speed.textContent = data.speed.toLocaleString();
            //把这两个时间改为了新的格式化时间
            elements.elapsed.textContent = formatTime(data.elapsed);
            if (data.speed > 0 && data.total) {
                const remainingSeeds = data.total - data.checkedCount;
                const remainingSeconds = remainingSeeds / data.speed;
                elements.eta.textContent = formatTime(remainingSeconds);
            } else {
                elements.eta.textContent = '∞';
            }
            const progressInt = Math.floor(data.progress);
            elements.progressBar.style.width = progressInt + '%';
            elements.progressBar.textContent = progressInt + '%';
            //把统计更新掉
            if (data.featureStats && data.featureStats.length > 0) {
                updateAnalysisUI(data.featureStats, data.checkedCount, foundSeeds.length);
            }
            break;

        case 'found':
            foundSeeds.push(data.seed);
            elements.foundCount.textContent = foundSeeds.length;

            // 缓存种子信息用于展示简介
            if (data.details) {
                seedDetailsCache[data.seed] = {
                    details: data.details,
                    enabled: data.enabledFeatures || {}  // 如果后端没发送，用空对象
                };
            }

            if (foundSeeds.length <= 20) {
                const seedItem = document.createElement('div');
                seedItem.className = 'seed-item';
                seedItem.innerHTML = `
                    <span>种子: ${data.seed}</span>
                    <div class="seed-item-actions">
                        <button class="btn-detail" onclick="showSeedDetail(${data.seed})">简介</button>
                        <button class="btn-copy" onclick="copySeed(${data.seed})">复制</button>
                    </div>
                `;
                elements.seedList.appendChild(seedItem);
            }

            updateResultsSummary();
            break;

        case 'complete':
            elements.statusMessage.textContent = data.cancelled
                ? `搜索已停止，共找到 ${data.totalFound} 个符合条件的种子`
                : `搜索完成！找到 ${data.totalFound} 个符合条件的种子`;
            elements.statusMessage.className = 'status-message status-complete';
            elements.searchBtn.disabled = false;
            elements.searchBtn.textContent = '🔍 开始搜索';
            elements.searchBtn.classList.remove('btn-stop');
            isSearching = false;

            const loopSearch = document.getElementById('loopSearch').checked;
            if (!data.cancelled && loopSearch) {
                const searchRange = parseInt(document.getElementById('searchRange').value);
                nextStartSeed += searchRange;
                document.getElementById('startSeed').value = nextStartSeed;
            }

            //判断有没有提前停止
            const maxOutputLimit = parseInt(document.getElementById('outputLimit').value) || 0;
            const hitLimit = (!data.cancelled && foundSeeds.length >= maxOutputLimit);
            const stopEl = document.getElementById('analysisStoppedEarly');
            stopEl.textContent = hitLimit ? '否' : '是'; //好像逻辑写反了，直接反向，嘿嘿
            stopEl.style.color = hitLimit ? '#d32f2f' : '#333';

            updateResultsSummary();
            break;
    }
}

function updateResultsSummary() {
    const total = foundSeeds.length;
    const shown = Math.min(total, 20);
    elements.resultsSummary.textContent = `共找到 ${total} 个 (显示前 ${shown} 个)`;
}

/**
 * 导出结果到 TXT 文件
 */
function exportResultsToTxt() {
    if (foundSeeds.length === 0) {
        alert("没有可导出的种子结果！");
        return;
    }

    // 1. 生成文件名：脆音音种子搜索器_日期_时间
    const now = new Date();
    const dateStr = now.getFullYear() +
        String(now.getMonth() + 1).padStart(2, '0') +
        String(now.getDate()).padStart(2, '0');
    const timeStr = String(now.getHours()).padStart(2, '0') +
        String(now.getMinutes()).padStart(2, '0') +
        String(now.getSeconds()).padStart(2, '0');
    const fileName = `脆音音种子搜索器_${dateStr}_${timeStr}.txt`;

    // 2. 生成条件总结头部
    let content = "========================================\n";
    content += "脆音音星露谷种子搜索结果\n";
    content += "========================================\n\n";
    content += "BUG反馈、新功能建议请联系作者：\n";
    content += "https://space.bilibili.com/349111916\n\n";
    content += `生成时间：${now.toLocaleString()}\n`;
    content += `搜索范围：${document.getElementById('startSeed').value} - ${Math.min(parseInt(document.getElementById('startSeed').value) + (document.getElementById('searchRange').value === 'max' ? 2147483647 : parseInt(document.getElementById('searchRange').value)) - 1, 2147483647)}\n`;
    content += `随机模式：${document.getElementById('useLegacy').checked ? "旧随机" : "新随机 "}\n`;
    content += `共找到种子：${foundSeeds.length} 个\n\n`;
    content += "----------- 筛选条件清单 -----------\n";

    // 提取各项条件
    content += getConditionsSummaryText();

    content += "\n----------- 种子列表 -----------\n";
    content += foundSeeds.join('\n');
    content += "\n\n========================================\n";

    // 3. 执行下载
    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
}

/**
 * 格式化输出当前启用的所有条件
 */
function getConditionsSummaryText() {
    let summary = "";
    const seasonNames = ["春", "夏", "秋", "冬"];

    // 天气
    if (document.getElementById('weatherEnabled').checked) {
        summary += "[天气]：\n";
        document.querySelectorAll('.weather-condition-row').forEach(row => {
            const s = row.querySelector('.weather-season-select').value;
            const start = row.querySelector('.weather-start-day').value;
            const end = row.querySelector('.weather-end-day').value;
            const rain = row.querySelector('.weather-min-rain').value;
            summary += `  - ${s}季${start}日 - ${end}日：至少 ${rain} 天下雨\n`;
        });
    }

    // 仙子
    if (document.getElementById('fairyEnabled').checked) {
        summary += "[仙子]：\n";
        document.querySelectorAll('.fairy-condition-row').forEach(row => {
            const sy = row.querySelector('.fairy-start-year').value;
            const ss = row.querySelector('.fairy-start-season').value;
            const sd = row.querySelector('.fairy-start-day').value;
            const ey = row.querySelector('.fairy-end-year').value;
            const es = row.querySelector('.fairy-end-season').value;
            const ed = row.querySelector('.fairy-end-day').value;
            summary += `  - 第${sy}年${ss}${sd}日 - 第${ey}年${es}${ed}日：范围内至少出现1次\n`;
        });
    }

    // 矿井宝箱
    if (document.getElementById('mineChestEnabled').checked) {
        summary += "[矿井混合宝箱]：\n";
        document.querySelectorAll('.minechest-condition-row').forEach(row => {
            const floor = row.querySelector('.minechest-floor').value;
            const item = row.querySelector('.minechest-item').value;
            summary += `  - 第 ${floor} 层宝箱物品：${item}\n`;
        });
    }

    // 怪物层
    if (document.getElementById('monsterLevelEnabled').checked) {
        summary += "[怪物层]：\n";
        document.querySelectorAll('.monsterlevel-condition-row').forEach(row => {
            const ss = row.querySelector('.monsterlevel-start-season').value;
            const sd = row.querySelector('.monsterlevel-start-day').value;
            const sLevel = row.querySelector('.monsterlevel-start-level').value;
            const eLevel = row.querySelector('.monsterlevel-end-level').value;
            summary += `  - ${ss}${sd}日：第 ${sLevel}-${eLevel} 层无怪物层\n`;
        });
    }

    // 沙漠节
    if (document.getElementById('desertFestivalEnabled').checked) {
        summary += "[沙漠节]：\n";
        if (document.getElementById('requireJas').checked) summary += "  - 必须出现：贾斯（魔法糖冰棍）\n";
        if (document.getElementById('requireLeah').checked) summary += "  - 必须出现：莉亚（100硬木）\n";
    }

    // 猪车
    if (document.getElementById('cartEnabled').checked) {
        summary += "[猪车]：\n";
        document.querySelectorAll('.cart-condition-row').forEach(row => {
            const sy = row.querySelector('.cart-start-year').value;
            const ss = row.querySelector('.cart-start-season').value;
            const sd = row.querySelector('.cart-start-day').value;
            const ey = row.querySelector('.cart-end-year').value;
            const es = row.querySelector('.cart-end-season').value;
            const ed = row.querySelector('.cart-end-day').value;
            const item = row.querySelector('.cart-item-select').value;
            const multi = row.querySelector('.cart-multi-check').checked;
            const count = row.querySelector('.cart-min-occurrences').value;
            const qty5 = row.querySelector('.cart-require-qty5').checked;
            summary += `  - 第${sy}年${ss}${sd}日 - 第${ey}年${es}${ed}日：${item}${qty5 ? '(5个)' : ''}，至少出售 ${multi ? count : 1} 次\n`;
        });
    }

    if (summary === "") summary = "未开启任何特定筛选条件（全随机搜索）\n";
    return summary;
}

elements.form.addEventListener('submit', async (e) => {
    e.preventDefault();

    // 如果正在搜索，点击按钮则停止搜索
    if (isSearching) {
        await fetch('http://localhost:5000/api/stop', { method: 'POST' });
        return;
    }

    const loopSearch = document.getElementById('loopSearch').checked;
    const useLegacy = document.getElementById('useLegacy').checked;
    currentSearchUseLegacy = useLegacy; // 保存当前搜索模式
    const outputLimit = parseInt(document.getElementById('outputLimit').value); // 读取输出数量

    // 天气
    const weatherEnabled = elements.weatherEnabled.checked;
    let weatherConditionsData = [];

    // 仙子
    const fairyEnabled = elements.fairyEnabled.checked;
    let fairyConditionsData = [];

    // 矿井宝箱
    const mineChestEnabled = elements.mineChestEnabled.checked;
    let mineChestConditionsData = [];

    // 怪物层
    const monsterLevelEnabled = document.getElementById('monsterLevelEnabled').checked;
    let monsterLevelConditionsData = [];

    // 沙漠节
    const desertFestivalEnabled = elements.desertFestivalEnabled.checked;
    const desertFestivalCondition = desertFestivalEnabled ? {
        requireJas: elements.requireJas.checked,
        requireLeah: elements.requireLeah.checked
    } : null;

    // 猪车
    const cartEnabled = elements.cartEnabled.checked;
    let cartConditionsData = [];

    // 计算起始种子
    let startSeed = loopSearch && nextStartSeed > 0
        ? nextStartSeed
        : parseInt(document.getElementById('startSeed').value);

    document.getElementById('startSeed').value = startSeed;

    // --- 核心修改：处理搜索范围的数值计算 ---
    let searchRange;
    const rangeValue = document.getElementById('searchRange').value;
    const INT_MAX = 2147483647;

    if (rangeValue === "max") {
        // 如果选了最大，范围就是从当前起点到 INT_MAX 的距离
        searchRange = INT_MAX - startSeed + 1;
    } else {
        searchRange = parseInt(rangeValue);
    }

    // 计算结束种子,不超过最大值
    const endSeed = Math.min(startSeed + searchRange - 1, INT_MAX);

    currentStartSeedDisplay = startSeed;
    currentEndSeedDisplay = endSeed;

    // 天气条件验证
    if (weatherEnabled) {
        const weatherRows = document.querySelectorAll('.weather-condition-row');

        if (weatherRows.length === 0) {
            alert('请至少添加一个天气条件！');
            return;
        }

        // 验证所有条件
        for (let row of weatherRows) {
            // 读取界面值
            const seasonName = row.querySelector('.weather-season-select').value;
            const startDay = parseInt(row.querySelector('.weather-start-day').value);
            const endDay = parseInt(row.querySelector('.weather-end-day').value);
            const minRain = parseInt(row.querySelector('.weather-min-rain').value);

            const condition = {
                season: SeasonNameToIndex[seasonName],
                startDay: startDay,
                endDay: endDay,
                minRainDays: minRain
            };

            // 验证合法性
            const validation = validateWeatherCondition(condition);
            if (!validation.valid) {
                alert(`天气错误: ${validation.error}`);
                return;
            }

            // 检查重叠 (利用绝对天数)
            if (hasWeatherOverlap(condition, weatherConditionsData)) {
                alert(`天气错误: [${seasonName}] 季日期范围存在重叠`);
                return;
            }

            weatherConditionsData.push(condition);
        }
    }

    // 仙子条件验证
    if (fairyEnabled) {
        const fairyRows = document.querySelectorAll('.fairy-condition-row');

        if (fairyRows.length === 0) {
            alert('请至少添加一个仙子条件！');
            return;
        }

        for (let row of fairyRows) {
            const condition = {
                startYear: parseInt(row.querySelector('.fairy-start-year').value),
                startSeason: SeasonNameToIndex[row.querySelector('.fairy-start-season').value],
                startDay: parseInt(row.querySelector('.fairy-start-day').value),
                endYear: parseInt(row.querySelector('.fairy-end-year').value),
                endSeason: SeasonNameToIndex[row.querySelector('.fairy-end-season').value],
                endDay: parseInt(row.querySelector('.fairy-end-day').value)
            };

            // 1. 基础合法性验证
            const validation = validateFairyCondition(condition);
            if (!validation.valid) {
                alert(`仙子搜索范围错误: ${validation.error}`);
                return;
            }

            // 2. 查重验证
            if (isFairyDuplicate(condition, fairyConditionsData)) {
                alert(`仙子搜索存在重复范围！`);
                return;
            }

            fairyConditionsData.push(condition);
        }
    }

    // 矿井宝箱验证
    if (mineChestEnabled) {
        const mineRows = document.querySelectorAll('.minechest-condition-row');
        const usedFloors = new Set(); // 用于检查重复

        if (mineRows.length === 0) {
            alert('请至少添加一个矿井宝箱条件！');
            return;
        }

        for (let row of mineRows) {
            const floor = parseInt(row.querySelector('.minechest-floor').value);
            const itemName = row.querySelector('.minechest-item').value;

            if (usedFloors.has(floor)) {
                alert(`错误：矿井第 ${floor} 层被重复设置了！`);
                return; // 终止搜索
            }

            usedFloors.add(floor);
            mineChestConditionsData.push({
                floor: floor,
                itemName: itemName
            });
        }
    }

    // 怪物层条件验证
    if (monsterLevelEnabled) {
        const monsterRows = document.querySelectorAll('.monsterlevel-condition-row');

        if (monsterRows.length === 0) {
            alert('请至少添加一个怪物层筛选条件！');
            return;
        }

        for (let row of monsterRows) {
            const condition = {
                // 默认第一年
                startSeason: SeasonNameToIndex[row.querySelector('.monsterlevel-start-season').value],
                endSeason: SeasonNameToIndex[row.querySelector('.monsterlevel-end-season').value],
                startDay: parseInt(row.querySelector('.monsterlevel-start-day').value),
                endDay: parseInt(row.querySelector('.monsterlevel-end-day').value),
                // 层数数据
                startLevel: parseInt(row.querySelector('.monsterlevel-start-level').value),
                endLevel: parseInt(row.querySelector('.monsterlevel-end-level').value)
            };

            // 验证
            const v = validateMonsterLevelCondition(condition);
            if (!v.valid) {
                alert(`怪物层错误: ${v.error}`);
                return;
            }

            monsterLevelConditionsData.push(condition);
        }
    }

    // 猪车条件验证
    if (cartEnabled) {
        const cartRows = document.querySelectorAll('.cart-condition-row');

        if (cartRows.length === 0) {
            alert('请至少添加一个猪车条件！');
            return;
        }

        for (let row of cartRows) {
            const multiCheck = row.querySelector('.cart-multi-check').checked;

            const condition = {
                startYear: parseInt(row.querySelector('.cart-start-year').value),
                startSeason: SeasonNameToIndex[row.querySelector('.cart-start-season').value],
                startDay: parseInt(row.querySelector('.cart-start-day').value),

                endYear: parseInt(row.querySelector('.cart-end-year').value),
                endSeason: SeasonNameToIndex[row.querySelector('.cart-end-season').value],
                endDay: parseInt(row.querySelector('.cart-end-day').value),

                itemName: row.querySelector('.cart-item-select').value,
                requireQty5: row.querySelector('.cart-require-qty5').checked,
                minOccurrences: multiCheck ? parseInt(row.querySelector('.cart-min-occurrences').value) : 1
            };

            // 验证合法性
            const validation = validateCartCondition(condition);
            if (!validation.valid) {
                alert(`猪车筛选错误: ${validation.error}`);
                return;
            }

            cartConditionsData.push(condition);
        }
    }

    // 显示进度区域
    elements.progressSection.style.display = 'block';
    elements.resultsSection.style.display = 'block';
    elements.searchBtn.disabled = false;
    elements.searchBtn.textContent = '⏹ 停止搜索';
    elements.searchBtn.classList.add('btn-stop');
    isSearching = true;

    // 更新状态消息(显示搜索范围)
    elements.statusMessage.textContent = `正在搜索: ${startSeed.toLocaleString()}-${endSeed.toLocaleString()}`;

    elements.statusMessage.className = 'status-message status-searching';
    elements.progressBar.style.width = '0%';
    elements.progressBar.textContent = '0%';

    elements.checkedCount.textContent = '0';
    elements.foundCount.textContent = '0';
    elements.speed.textContent = '0';
    elements.elapsed.textContent = '0.0s';
    if (elements.eta) elements.eta.textContent = '-.-';

    // 发送搜索请求
    try {
        const response = await fetch('http://localhost:5000/api/search', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                startSeed,
                endSeed,
                useLegacyRandom: useLegacy,
                weatherConditions: weatherConditionsData,
                fairyConditions: fairyConditionsData,
                MineChestConditions: mineChestConditionsData,
                monsterLevelConditions: monsterLevelConditionsData,
                desertFestivalCondition: desertFestivalCondition,
                cartConditions: cartConditionsData,
                outputLimit // 将输出数量添加到请求中
            })
        });

        if (!response.ok) {
            throw new Error('搜索请求失败');
        }
    } catch (error) {
        console.error('搜索错误:', error);
        alert('搜索失败,请确保后端服务正在运行!');
        elements.searchBtn.disabled = false;
        elements.searchBtn.textContent = '🔍 开始搜索';
        elements.searchBtn.classList.remove('btn-stop');
        isSearching = false;
    }
});


// 显示种子详情
function showSeedDetail(seed) {
    const cached = seedDetailsCache[seed];
    if (!cached) return;

    const { details, enabled } = cached;
    const seasonNames = ["春", "夏", "秋", "冬"];

    document.getElementById('sidebarMode').textContent = currentSearchUseLegacy ? '旧随机' : '新随机';
    document.getElementById('sidebarSeedNumber').textContent = seed;

    // 只有启用了天气功能才显示
    if (enabled.weather && details.weather) {
        const seasonNames = ['春', '夏', '秋'];
        const seasons = [
            { name: seasonNames[0], days: details.weather.springRain, greenRainDay: null },
            { name: seasonNames[1], days: details.weather.summerRain, greenRainDay: details.weather.greenRainDay },
            { name: seasonNames[2], days: details.weather.fallRain, greenRainDay: null }
        ];

        let weatherHtml = '';  // 定义变量
        seasons.forEach(season => {
            const count = season.days.length;
            let daysText = '';

            if (count > 0) {
                daysText = season.days.map(day => {
                    const isGreenRain = season.greenRainDay === day;
                    return isGreenRain ? `<span class="green-rain">${day}（绿雨）</span>` : day;
                }).join(', ');
            }

            weatherHtml += `
                <div class="weather-season">
                    <div class="weather-season-title">${season.name}（${count}个）：</div>
                    <div class="weather-days">${count > 0 ? daysText : '无'}</div>
                </div>
            `;
        });

        document.getElementById('sidebarWeatherContent').innerHTML = weatherHtml;
        document.getElementById('weatherSection').style.display = 'block';
    } else {
        document.getElementById('weatherSection').style.display = 'none';
    }

    // 只有启用了仙子功能才显示
    if (enabled.fairy && details.fairy && details.fairy.days) {
        const fairyText = details.fairy.days.map(f => {
            const prefix = f.year === 1 ? '' : `${f.year}年`;
            return `${prefix}${SeasonNames[f.season]}${f.day}`;
        }).join('、');

        const fairyHtml = `
            <div class="weather-season">
                <div class="weather-season-title">仙子（${details.fairy.days.length}个）：</div>
                <div class="weather-days">${fairyText}</div>
            </div>
        `;
        document.getElementById('sidebarFairyContent').innerHTML = fairyHtml;
        document.getElementById('fairySection').style.display = 'block';
    } else {
        document.getElementById('fairySection').style.display = 'none';
    }

    // 只有启用了矿井宝箱功能才显示
    if (enabled.mineChest && details.mineChest) {
        let chestHtml = '<div class="weather-season">';

        details.mineChest.forEach(item => {
            const matchIcon = item.matched ? '✓' : '✗';
            const matchClass = item.matched ? 'matched' : 'unmatched';
            chestHtml += `
                <div class="minechest-item ${matchClass}">
                    <span>${matchIcon} ${item.floor}层：${item.item}</span>
                </div>
            `;
        });
        chestHtml += '</div>';
        document.getElementById('sidebarMineChestContent').innerHTML = chestHtml;
        document.getElementById('mineChestSection').style.display = 'block';
    } else {
        document.getElementById('mineChestSection').style.display = 'none';
    }

    // 只有启用了怪物层功能才显示
    if (enabled.monsterLevel && details.monsterLevel) {
        const seasonMap = { Spring: '春', Summer: '夏', Fall: '秋', Winter: '冬' };
        const monsterLevelText = details.monsterLevel.map(m => {
            return m.description;
        }).join('<br>');

        const monsterLevelHtml = `
            <div class="weather-season">
                <div class="weather-days">${monsterLevelText}</div>
            </div>
        `;
        document.getElementById('sidebarMonsterLevelContent').innerHTML = monsterLevelHtml;
        document.getElementById('monsterLevelSection').style.display = 'block';
    } else {
        document.getElementById('monsterLevelSection').style.display = 'none';
    }

    // 只有启用了沙漠节功能才显示
    if (enabled.desertFestival && details.desertFestival) {
        const vendorNameMap = {
            'Abigail': '阿比盖尔', 'Caroline': '卡洛琳', 'Clint': '克林特',
            'Demetrius': '德米特里厄斯', 'Elliott': '艾利欧特', 'Emily': '艾米丽',
            'Evelyn': '艾芙琳', 'George': '乔治', 'Gus': '格斯',
            'Haley': '海莉', 'Harvey': '哈维', 'Jas': '贾斯',
            'Jodi': '乔迪', 'Alex': '亚历克斯', 'Kent': '肯特',
            'Leah': '莉亚', 'Marnie': '玛妮', 'Maru': '玛鲁',
            'Pam': '潘姆', 'Penny': '潘妮', 'Pierre': '皮埃尔',
            'Robin': '罗宾', 'Sam': '山姆', 'Sebastian': '塞巴斯蒂安',
            'Shane': '谢恩', 'Vincent': '文森特', 'Leo': '雷欧'
        };

        // 在 map 转换中文名之后，再处理高亮
        const highlightVendor = (name) => {
            if (name === '贾斯') return `<span style="color: #9b59b6; font-weight: bold;">${name}</span>`;
            if (name === '莉亚') return `<span style="color: #ff8c00; font-weight: bold;">${name}</span>`;
            return name;
        };

        const day15Vendors = details.desertFestival.day15
            .map(v => highlightVendor(vendorNameMap[v] || v)).join('、');
        const day16Vendors = details.desertFestival.day16
            .map(v => highlightVendor(vendorNameMap[v] || v)).join('、');
        const day17Vendors = details.desertFestival.day17
            .map(v => highlightVendor(vendorNameMap[v] || v)).join('、');

        const desertFestivalHtml = `
            <div class="weather-season">
                <div class="weather-season-title">春15：</div>
                <div class="weather-days">${day15Vendors}</div>
            </div>
            <div class="weather-season">
                <div class="weather-season-title">春16：</div>
                <div class="weather-days">${day16Vendors}</div>
            </div>
            <div class="weather-season">
                <div class="weather-season-title">春17：</div>
                <div class="weather-days">${day17Vendors}</div>
            </div>
        `;

        document.getElementById('sidebarDesertFestivalContent').innerHTML = desertFestivalHtml;
        document.getElementById('desertFestivalSection').style.display = 'block';
    } else {
        document.getElementById('desertFestivalSection').style.display = 'none';
    }

    // 只有启用了猪车功能才显示
    if (enabled.cart && details.cart && details.cart.matches && details.cart.matches.length > 0) {

        // 1. 按AbsoluteDay升序排序，确保展示顺序正确
        const sortedMatches = [...details.cart.matches].sort((a, b) => a.AbsoluteDay - b.AbsoluteDay);

        // 2. 格式化每一行数据
        const cartRowsHtml = sortedMatches.map(match => {
            // 获取季节名
            const seasonName = seasonNames[match.Season] || "未知";

            // 如果数量为 -1（技能书），显示为空；否则显示数字
            const qtyDisplay = (match.Quantity === -1) ? "" : match.Quantity;

            // 拼接单行：第1年春7，电池组5，2000g
            console.log("details:", details);
            return `<div class="cart-result-line">
                第${match.Year}年${seasonName}${match.Day}，${match.ItemName}${qtyDisplay}，${match.Price}g
    </div>`;
        }).join('');

        // 3. 构建整体 HTML 结构
        const cartHtml = `
            <div class="weather-season">
                <div class="weather-season-title">猪车匹配结果：</div>
                <div class="cart-results-list" style="margin-top: 8px; font-size: 16px; line-height: 1.6;">
                    ${cartRowsHtml}
                </div>
            </div>
        `;

        elements.sidebarCartContent.innerHTML = cartHtml;
        elements.cartSection.style.display = 'block';
    } else {
        elements.cartSection.style.display = 'none';
    }

    // 显示侧边栏
    document.getElementById('sidebarPanel').classList.add('active');
}

// 关闭侧边栏
function closeSidebar() {
    document.getElementById('sidebarPanel').classList.remove('active');
}

// 复制种子号
function copySeed(seed) {
    navigator.clipboard.writeText(seed).then(() => {
        showCopyToast();
    });
}

// 从侧边栏复制
function copySeedFromSidebar() {
    console.log('复制按钮被点击了');
    const seed = document.getElementById('sidebarSeedNumber').textContent;
    console.log('种子号:', seed);
    navigator.clipboard.writeText(seed).then(() => {
        showCopyToast();
    });
}

// 显示复制提示
function showCopyToast() {
    const toast = document.getElementById('copyToast');
    toast.classList.add('show');
    setTimeout(() => {
        toast.classList.remove('show');
    }, 2000);
}

document.addEventListener('DOMContentLoaded', function () {
    const INT_MIN = 1;
    const INT_MAX = 2147483647;
    const RANDOM_MAX = 2000000000;

    const startSeedInput = document.getElementById('startSeed');
    const btnZeroSeed = document.getElementById('btnZeroSeed');
    const btnRandomSeed = document.getElementById('btnRandomSeed'); // 随机起始种子

    function enforceIntLimits(inputElement) {
        let val = parseInt(inputElement.value);
        if (isNaN(val)) return;
        if (val > INT_MAX) inputElement.value = INT_MAX;
        else if (val < INT_MIN) inputElement.value = INT_MIN;
    }

    if (startSeedInput) startSeedInput.addEventListener('blur', function () { enforceIntLimits(this); });

    // 一键最小起始种子
    if (btnZeroSeed) {
        btnZeroSeed.addEventListener('click', function () {
            startSeedInput.value = 1;
            // 触发 change 事件以同步 nextStartSeed
            startSeedInput.dispatchEvent(new Event('change'));
        });
    }

    // 一键随机起始种子
    if (btnRandomSeed) {
        btnRandomSeed.addEventListener('click', function () {
            // 生成 1 到 20e 的随机数
            const randomSeed = Math.floor(Math.random() * RANDOM_MAX) + 1;
            startSeedInput.value = randomSeed;
            startSeedInput.dispatchEvent(new Event('change'));
        });
    }
});

function updateAnalysisUI(stats, currentChecked, totalFound) {
    document.getElementById('analysisTotalRange').textContent = currentChecked.toLocaleString();
    document.getElementById('analysisTotalFound').textContent = totalFound.toLocaleString();

    const container = document.getElementById('filterStatsList');
    if (!container || !stats) return;

    let html = '';
    let prevCount = currentChecked;

    stats.forEach(stat => {
        let rawRate = prevCount > 0 ? (stat.passCount / prevCount * 100) : 0; //这是展示在通过第前关的基础上的种子，在当前关卡的通过率
        // let rawRate = currentChecked > 0 ? (stat.passCount / currentChecked * 100) : 0;  音师傅如果想改成总体的通过率就用这行，我之前写的通过/总数得到零点几，展示出来我觉得没意义了
        let displayRate = rawRate % 1 === 0 ? rawRate.toFixed(0) : rawRate.toFixed(1);

        const hue = Math.min(rawRate * 1.2, 120);

        html += `
            <div class="analysis-row">
                <div class="analysis-item-name">${stat.name}</div>
                <div class="analysis-item-count">${stat.passCount.toLocaleString()}个种子</div>
                <div class="analysis-rate-box" style="border-left-color: hsl(${hue}, 70%, 50%);">
                    <span>通过率:</span>
                    <span>${displayRate}%</span>
                </div>
            </div>
        `;
        //下一关的起点是本关留存的种子
        prevCount = stat.passCount;
    });

    container.innerHTML = html;
}

connectWebSocket();