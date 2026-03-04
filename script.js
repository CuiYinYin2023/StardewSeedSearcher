let ws = null;
let foundSeeds = [];
let conditions = [];
let currentSearchUseLegacy = false;
let seedDetailsCache = {};
let nextStartSeed = 0;
let fairyConditions = [];
let nextFairyIndex = 1;
let mineChestConditions = [];
let nextMineChestIndex = 1;
let monsterLevelConditions = [];
let nextMonsterLevelIndex = 1;
let ALL_CART_ITEM_NAMES = [];
let cartConditions = [];
let nextCartIndex = 1;

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

const MINE_CHEST_ITEMS = { 
    10: ["皮靴", "工作靴", "木剑", "铁制短剑", "疾风利剑", "股骨"],
    20: ["钢制轻剑", "木棒", "精灵之刃", "光辉戒指", "磁铁戒指"],
    50: ["冻土靴", "热能靴", "战靴", "镀银军刀", "海盗剑"],
    60: ["水晶匕首", "弯刀", "铁刃", "飞贼之胫", "木锤"],
    80: ["蹈火者靴", "黑暗之靴", "双刃大剑", "圣堂之刃", "长柄锤", "暗影匕首"],
    90: ["黑曜石之刃", "淬火阔剑", "蛇形邪剑", "骨剑", "骨化剑"],
    110: ["太空之靴", "水晶鞋", "钢刀", "巨锤"]
};

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

// 添加条件
function addCondition() {
    const container = document.getElementById('conditionsContainer');
    
    const newRow = document.createElement('div');
    newRow.className = 'condition-row';
    newRow.innerHTML = `
        <select>
            <option value="Spring">春</option>
            <option value="Summer">夏</option>
            <option value="Fall">秋</option>
        </select>
        <input type="number" placeholder="起始 (1-28)" min="1" max="28" value="1">
        <input type="number" placeholder="结束 (1-28)" min="1" max="28" value="28">
        <input type="number" placeholder="最少雨天" min="1" value="10">
        <button type="button" class="btn-remove">删除</button>
    `;
    
    container.appendChild(newRow);
    
    // 添加删除事件
    newRow.querySelector('.btn-remove').addEventListener('click', function() {
        newRow.remove();
        syncConditions();
    });
    
    // 添加输入变化事件
    newRow.querySelectorAll('select, input').forEach(el => {
        el.addEventListener('change', syncConditions);
    });
    
    syncConditions();
    hideError();
}

// 同步条件数据（从DOM读取）
function syncConditions() {
    const rows = document.querySelectorAll('.condition-row');
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
function validateCondition(condition) {
    if (!condition) return { valid: false, error: '条件数据缺失' };
    
    const { startDay, endDay, minRain } = condition;
    
    if (startDay < 1 || startDay > 28 || endDay < 1 || endDay > 28) {
        return { valid: false, error: '日期必须在 1-28 之间' };
    }
    
    if (startDay > endDay) {
        return { valid: false, error: '起始日期必须小于等于结束日期' };
    }
    
    const dayCount = endDay - startDay + 1;
    if (minRain < 1 || minRain > dayCount) {
        return { valid: false, error: `最少雨天数必须在 1-${dayCount} 之间` };
    }
    
    return { valid: true };
}

// 检查条件重叠
function hasOverlap(newCondition, excludeIndex) {
    const seasonOffset = { 'Spring': 0, 'Summer': 28, 'Fall': 56 };
    const newStart = seasonOffset[newCondition.season] + newCondition.startDay;
    const newEnd = seasonOffset[newCondition.season] + newCondition.endDay;

    for (let i = 0; i < conditions.length; i++) {
        if (i === excludeIndex) continue;
        
        const cond = conditions[i];
        const start = seasonOffset[cond.season] + cond.startDay;
        const end = seasonOffset[cond.season] + cond.endDay;

        if (!(newEnd < start || end < newStart)) {
            return true;
        }
    }
    return false;
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
    const index = nextFairyIndex++;
    
    // 克隆模板
    const clone = template.content.cloneNode(true);
    const row = clone.querySelector('.fairy-condition-row');
    row.setAttribute('data-index', index);
    
    // 绑定事件
    row.querySelector('.fairy-year').onchange = () => updateFairyCondition(index);
    row.querySelector('.fairy-season').onchange = () => updateFairyCondition(index);
    row.querySelector('.fairy-day').onchange = () => updateFairyCondition(index);
    row.querySelector('.btn-remove').onclick = () => removeFairyCondition(index);
    
    container.appendChild(clone);
    updateFairyCondition(index);
}

function updateFairyCondition(index) {
    const row = document.querySelector(`.fairy-condition-row[data-index="${index}"]`);
    if (!row) return;
    
    const year = parseInt(row.querySelector('.fairy-year').value) || 1;
    const season = row.querySelector('.fairy-season').value;
    const day = parseInt(row.querySelector('.fairy-day').value) || 1;
    
    fairyConditions[index] = { year, season, day };
}

function removeFairyCondition(index) {
    const row = document.querySelector(`.fairy-condition-row[data-index="${index}"]`);
    if (row) {
        row.remove();
        delete fairyConditions[index];
    }
}

function validateFairyCondition(condition) {
    const { year, day } = condition;
    
    if (year < 1 || year > 10) {
        return { valid: false, error: '年份必须在 1-10 之间' };
    }
    
    if (day < 1 || day > 28) {
        return { valid: false, error: '日期必须在 1-28 之间' };
    }
    
    return { valid: true };
}

function hasFairyDuplicate(newCondition, excludeIndex) {
    const validConditions = fairyConditions.filter((c, i) => c && i !== excludeIndex);
    
    for (let condition of validConditions) {
        if (condition.year === newCondition.year && 
            condition.season === newCondition.season && 
            condition.day === newCondition.day) {
            return true;
        }
    }
    return false;
}

// 添加矿井宝箱条件
function addMineChestCondition() {
    const container = document.getElementById('mineChestConditionsContainer');
    const template = document.getElementById('mineChestConditionTemplate');
    const index = nextMineChestIndex++;
    
    const clone = template.content.cloneNode(true);
    const row = clone.querySelector('.minechest-condition-row');
    row.setAttribute('data-index', index);
    
    const floorSelect = row.querySelector('.minechest-floor');
    const itemSelect = row.querySelector('.minechest-item');
    
    // 默认填充第一个楼层的物品
    populateItemOptions(itemSelect, 10);
    
    // 绑定事件
    floorSelect.onchange = () => onFloorChange(index);  // 使用新函数
    itemSelect.onchange = () => updateMineChestCondition(index);
    row.querySelector('.btn-remove').onclick = () => removeMineChestCondition(index);
    
    container.appendChild(clone);
    updateMineChestCondition(index);
}

// 更新矿井宝箱条件
function updateMineChestCondition(index) {
    const row = document.querySelector(`.minechest-condition-row[data-index="${index}"]`);
    if (!row) return;
    
    const floor = parseInt(row.querySelector('.minechest-floor').value);
    const item = row.querySelector('.minechest-item').value;
    
    mineChestConditions[index] = { Floor: floor, ItemName: item };
}

// 删除矿井宝箱条件
function removeMineChestCondition(index) {
    const row = document.querySelector(`.minechest-condition-row[data-index="${index}"]`);
    if (row) {
        row.remove();
        delete mineChestConditions[index];
    }
}

// 根据楼层填充物品选项
function populateItemOptions(selectElement, floor) {
    selectElement.innerHTML = ''; // 清空现有选项
    const items = MINE_CHEST_ITEMS[floor] || [];
    
    items.forEach(item => {
        const option = document.createElement('option');
        option.value = item;
        option.textContent = item;
        selectElement.appendChild(option);
    });
}

// 检查重复条件
function hasMineChestDuplicate(newCondition, excludeIndex) {
    const validConditions = mineChestConditions.filter((c, i) => c && i !== excludeIndex);
    
    for (let condition of validConditions) {
        if (condition.floor === newCondition.floor && 
            condition.ItemName === newCondition.ItemName) {
            return true;
        }
    }
    return false;
}

// 处理楼层变化
function onFloorChange(index) {
    const row = document.querySelector(`.minechest-condition-row[data-index="${index}"]`);
    if (!row) return;
    
    const floorSelect = row.querySelector('.minechest-floor');
    const itemSelect = row.querySelector('.minechest-item');
    const floor = parseInt(floorSelect.value);
    
    // 更新物品列表
    populateItemOptions(itemSelect, floor);
    
    // 更新条件
    updateMineChestCondition(index);
}

// 怪物层相关函数
function addMonsterLevelCondition() {
    const container = document.getElementById('monsterLevelConditionsContainer');
    const template = document.getElementById('monsterLevelConditionTemplate');
    const index = nextMonsterLevelIndex++;
    
    // 克隆模板
    const clone = template.content.cloneNode(true);
    const row = clone.querySelector('.monsterlevel-condition-row');
    row.setAttribute('data-index', index);
    
    // 绑定事件
    row.querySelector('.monsterlevel-start-season').onchange = () => updateMonsterLevelCondition(index);
    row.querySelector('.monsterlevel-start-day').onchange = () => updateMonsterLevelCondition(index);
    row.querySelector('.monsterlevel-end-season').onchange = () => updateMonsterLevelCondition(index);
    row.querySelector('.monsterlevel-end-day').onchange = () => updateMonsterLevelCondition(index);
    row.querySelector('.monsterlevel-start-level').onchange = () => updateMonsterLevelCondition(index);
    row.querySelector('.monsterlevel-end-level').onchange = () => updateMonsterLevelCondition(index);
    row.querySelector('.btn-remove').onclick = () => removeMonsterLevelCondition(index);
    
    container.appendChild(clone);
    updateMonsterLevelCondition(index);
}

function updateMonsterLevelCondition(index) {
    const row = document.querySelector(`.monsterlevel-condition-row[data-index="${index}"]`);
    if (!row) return;
    
    const startSeason = row.querySelector('.monsterlevel-start-season').value;
    const startDay = parseInt(row.querySelector('.monsterlevel-start-day').value) || 5;
    const endSeason = row.querySelector('.monsterlevel-end-season').value;
    const endDay = parseInt(row.querySelector('.monsterlevel-end-day').value) || 5;
    const startLevel = parseInt(row.querySelector('.monsterlevel-start-level').value) || 1;
    const endLevel = parseInt(row.querySelector('.monsterlevel-end-level').value) || 80;
    
    // 转换为绝对天数
    const seasonMap = { Spring: 0, Summer: 1, Fall: 2, Winter: 3 };
    const startDayAbsolute = seasonMap[startSeason] * 28 + startDay;
    const endDayAbsolute = seasonMap[endSeason] * 28 + endDay;
    
    monsterLevelConditions[index] = {
        startDay: startDayAbsolute,
        endDay: endDayAbsolute,
        startLevel: startLevel,
        endLevel: endLevel
    };
}

function removeMonsterLevelCondition(index) {
    const row = document.querySelector(`.monsterlevel-condition-row[data-index="${index}"]`);
    if (row) {
        row.remove();
        delete monsterLevelConditions[index];
    }
}

function validateMonsterLevelCondition(condition) {
    const { startDay, endDay, startLevel, endLevel } = condition;
    
    if (startDay < 1 || startDay > 112) {
        return { valid: false, error: '起始日期必须在第一年范围内' };
    }
    
    if (endDay < 1 || endDay > 112) {
        return { valid: false, error: '结束日期必须在第一年范围内' };
    }
    
    if (startDay > endDay) {
        return { valid: false, error: '起始日期不能晚于结束日期' };
    }
    
    if (startLevel < 1 || startLevel > 120) {
        return { valid: false, error: '起始层数必须在 1-120 之间' };
    }
    
    if (endLevel < 1 || endLevel > 120) {
        return { valid: false, error: '结束层数必须在 1-120 之间' };
    }
    
    if (startLevel > endLevel) {
        return { valid: false, error: '起始层数不能大于结束层数' };
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
    const index = nextCartIndex++;
    
    const clone = template.content.cloneNode(true);
    const row = clone.querySelector('.cart-condition-row');
    row.setAttribute('data-index', index);
    
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
        updateCartCondition(index); // 触发数据更新
    });
    
    // 为当前行内所有的 input (year, day, itemName, checkbox) 和 select (season) 绑定监听
    row.querySelectorAll('select, input').forEach(input => {
        input.addEventListener('input', () => updateCartCondition(index));
    });

    // 移除按钮
    row.querySelector('.btn-remove').onclick = () => removeCartCondition(index);
    
    container.appendChild(clone);
    updateCartCondition(index); // 初始化当前行数据
}

// 更新指定索引的猪车条件数据
function updateCartCondition(index) {
    const row = document.querySelector(`.cart-condition-row[data-index="${index}"]`);
    if (!row) return;
    
    const startYear = row.querySelector('.cart-start-year').value;
    const startSeason = row.querySelector('.cart-start-season').value;
    const startDay = parseInt(row.querySelector('.cart-start-day').value);
    const endYear = row.querySelector('.cart-end-year').value;
    const endSeason = row.querySelector('.cart-end-season').value;
    const endDay = parseInt(row.querySelector('.cart-end-day').value);
    const itemName = row.querySelector('.cart-item-select').value;
    const requireQty5 = row.querySelector('.cart-require-qty5').checked;
    
    cartConditions[index] = { 
        startYear,
        startSeason, 
        startDay, 
        endYear,
        endSeason, 
        endDay,
        itemName,
        requireQty5
    };
    
    // 清除错误消息
    elements.cartConditionError.textContent = '';
}

// 移除指定索引的猪车条件行
function removeCartCondition(index) {
    const row = document.querySelector(`.cart-condition-row[data-index="${index}"]`);
    if (row) {
        row.remove();
        delete cartConditions[index];
    }
}


// 验证猪车条件
function validateCartCondition(condition) {
    const { startYear, startSeason, startDay, endYear, endSeason, endDay, itemName } = condition;
    
    // 1. 年份验证
    if (startYear < 1 || endYear < 1) {
        return { valid: false, error: '年份不能小于 1' };
    }

    // 2. 日期验证
    if (startDay < 1 || startDay > 28 || endDay < 1 || endDay > 28) {
        return { valid: false, error: '日期必须在 1-28 之间' };
    }
    
    // 3. 物品名验证（只需要检查是否选了值）
    if (!itemName || itemName === "") {
        return { valid: false, error: '请在下拉菜单中选择一个具体的物品' };
    }
    
    // 4. 跨年绝对日期验证 (一年 112 天)
    const seasonOrder = { 'Spring': 0, 'Summer': 1, 'Fall': 2, 'Winter': 3 };
    const startAbs = ((startYear - 1) * 112) + (seasonOrder[startSeason] * 28) + startDay;
    const endAbs = ((endYear - 1) * 112) + (seasonOrder[endSeason] * 28) + endDay;

    if (startAbs > endAbs) {
        return { valid: false, error: '起始日期不能晚于结束日期' };
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
    const searchRangeInput = document.getElementById('searchRange');
    const outputLimitInput = document.getElementById('outputLimit');

    const range = parseInt(searchRangeInput.value) || 0;
    const limit = parseInt(outputLimitInput.value) || 0;

    if (range <= limit) {
        // 如果当前值超过了新的最大值，就把它降下来
        outputLimitInput.value = range;
    }
}

document.addEventListener('DOMContentLoaded', function() {
    // 天气条件初始化
    const firstRow = document.querySelector('.condition-row');
    if (firstRow) {
        // 为第一行添加删除事件
        firstRow.querySelector('.btn-remove').addEventListener('click', function() {
            firstRow.remove();
            syncConditions();
        });
        
        // 为第一行添加输入变化事件
        firstRow.querySelectorAll('select, input').forEach(el => {
            el.addEventListener('change', syncConditions);
        });
        
        syncConditions();
    }

    // 仙子条件初始化
    updateFairyCondition(0);

    // 矿井宝箱条件初始化
    const firstMineChestRow = document.querySelector('.minechest-condition-row[data-index="0"]');
    if (firstMineChestRow) {
        const floorSelect = firstMineChestRow.querySelector('.minechest-floor');
        const itemSelect = firstMineChestRow.querySelector('.minechest-item');
        
        // 设置默认楼层为 110
        floorSelect.value = '110';
        
        // 填充 110 层的物品列表
        populateItemOptions(itemSelect, 110);
        
        // 设置默认物品为"巨锤"
        itemSelect.value = '巨锤';
        
        // 绑定楼层变化事件
        floorSelect.onchange = () => onFloorChange(0);
        
        // 初始化条件
        updateMineChestCondition(0);
    }

    // 怪物层条件初始化
    updateMonsterLevelCondition(0);
    
    // 猪车条件初始化
    loadCartItems();
    initializeCartItemList(); // 初始化物品 datalist
    addCartCondition(); // 添加第一个条件行
});

// 监听起始种子修改,重置循环
document.getElementById('startSeed').addEventListener('change', function() {
    nextStartSeed = parseInt(this.value) || 0;
});

// 监听搜索范围修改,重置循环
document.getElementById('searchRange').addEventListener('change', function() {
    const startSeed = parseInt(document.getElementById('startSeed').value) || 0;
    nextStartSeed = startSeed;
});

// 监听循环搜索复选框
document.getElementById('loopSearch').addEventListener('change', function() {
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
            break;

        case 'progress':
            elements.checkedCount.textContent = data.checkedCount.toLocaleString();
            elements.speed.textContent = data.speed.toLocaleString();
            elements.elapsed.textContent = data.elapsed + 's';
            const progressInt = Math.floor(data.progress);
            elements.progressBar.style.width = progressInt + '%';
            elements.progressBar.textContent = progressInt + '%';
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
            elements.statusMessage.textContent = `搜索完成！找到 ${data.totalFound} 个符合条件的种子`;
            elements.statusMessage.className = 'status-message status-complete';
            elements.searchBtn.disabled = false;
            elements.searchBtn.textContent = '🔍 开始搜索';

            const loopSearch = document.getElementById('loopSearch').checked;
            if (loopSearch) {
                const searchRange = parseInt(document.getElementById('searchRange').value);
                nextStartSeed += searchRange;
                
                if (nextStartSeed > 2147483647) {
                    document.getElementById('loopSearch').checked = false;
                    alert('已搜索完所有种子范围');
                }
            }

            updateResultsSummary();
            break;
    }
}

function updateResultsSummary() {
    const total = foundSeeds.length;
    const shown = Math.min(total, 20);
    elements.resultsSummary.textContent = `共找到 ${total} 个 (显示前 ${shown} 个)`;
}
elements.form.addEventListener('submit', async (e) => {
    e.preventDefault();

    const loopSearch = document.getElementById('loopSearch').checked;
    const searchRange = parseInt(document.getElementById('searchRange').value);
    const useLegacy = document.getElementById('useLegacy').checked;
    currentSearchUseLegacy = useLegacy;  // 保存当前搜索模式
    const weatherEnabled = elements.weatherEnabled.checked;
    const outputLimit = parseInt(document.getElementById('outputLimit').value); // 读取输出数量
    const mineChestEnabled = elements.mineChestEnabled.checked;
    const desertFestivalEnabled = elements.desertFestivalEnabled.checked;
    const desertFestivalCondition = desertFestivalEnabled ? {
        requireJas: elements.requireJas.checked,
        requireLeah: elements.requireLeah.checked
    } : null;
    const cartEnabled = elements.cartEnabled.checked;
    const validCartConditions = cartConditions.filter(c => c);

    // 计算起始种子
    let startSeed = loopSearch && nextStartSeed > 0 
        ? nextStartSeed 
        : parseInt(document.getElementById('startSeed').value);

    // 更新输入框显示
    document.getElementById('startSeed').value = startSeed;
    
    // 计算结束种子,不超过最大值
    const endSeed = Math.min(startSeed + searchRange - 1, 2147483647);
    
    // 检查是否已到最大值
    if (startSeed >= 2147483647) {
        alert('已达到最大种子值,无法继续搜索');
        return;
    }

    // 检查搜索范围是否有效
    if (searchRange < 1) {
        alert('搜索范围必须大于0!');
        return;
    }

    // 天气条件验证
    if (weatherEnabled) {
        hideError();
        
        const validConditions = conditions.filter(c => c);
        
        if (validConditions.length === 0) {
            showError('请至少添加一个条件');
            return;
        }
        
        // 验证所有条件
        for (let i = 0; i < conditions.length; i++) {
            const condition = conditions[i];
            if (!condition) continue;
            
            const validation = validateCondition(condition);
            if (!validation.valid) {
                showError(`条件 ${i + 1}: ${validation.error}`);
                return;
            }
            
            if (hasOverlap(condition, i)) {
                showError(`条件 ${i + 1}: 与其他条件的日期范围重叠`);
                return;
            }
        }
    }

    // 仙子条件验证
    const fairyEnabled = elements.fairyEnabled.checked;

    if (fairyEnabled) {
        const validFairyConditions = fairyConditions.filter(c => c);
        
        if (validFairyConditions.length === 0) {
            alert('请至少添加一个仙子条件！');
            return;
        }
        
        for (let i = 0; i < fairyConditions.length; i++) {
            const condition = fairyConditions[i];
            if (!condition) continue;
            
            const validation = validateFairyCondition(condition);
            if (!validation.valid) {
                alert(`仙子条件 ${i + 1}: ${validation.error}`);
                return;
            }
            
            if (hasFairyDuplicate(condition, i)) {
                alert(`仙子条件 ${i + 1}: 与其他条件重复`);
                return;
            }
        }
    } 
    
    // 矿井宝箱验证
    if (mineChestEnabled) {
        const validMineChestConditions = mineChestConditions.filter(c => c);
        
        if (validMineChestConditions.length === 0) {
            alert('请至少添加一个矿井宝箱条件！');
            return;
        }
        
        for (let i = 0; i < mineChestConditions.length; i++) {
            const condition = mineChestConditions[i];
            if (!condition) continue;
            
            if (hasMineChestDuplicate(condition, i)) {
                alert(`矿井宝箱条件 ${i + 1}: 与其他条件重复`);
                return;
            }
        }
    }

    // 怪物层条件验证
    const monsterLevelEnabled = elements.monsterLevelEnabled.checked;

    if (monsterLevelEnabled) {
        const validMonsterLevelConditions = monsterLevelConditions.filter(c => c);
        
        if (validMonsterLevelConditions.length === 0) {
            alert('请至少添加一个怪物层条件！');
            return;
        }
        
        for (let i = 0; i < monsterLevelConditions.length; i++) {
            const condition = monsterLevelConditions[i];
            if (!condition) continue;
            
            const validation = validateMonsterLevelCondition(condition);
            if (!validation.valid) {
                alert(`怪物层条件 ${i + 1}: ${validation.error}`);
                return;
            }
        }
    }

    // 猪车条件验证
    if (cartEnabled) {
        if (validCartConditions.length === 0) {
            alert('请至少添加一个猪车条件！');
            return;
        }
        
        for (let i = 0; i < cartConditions.length; i++) {
            const condition = cartConditions[i];
            if (!condition) continue;
            
            const validation = validateCartCondition(condition);
            if (!validation.valid) {
                alert(`猪车条件 ${i + 1}: ${validation.error}`);
                return;
            }
            // 假设您有 hasCartDuplicate 检查重复的逻辑
        }
    }
    // 显示进度区域
    elements.progressSection.style.display = 'block';
    elements.resultsSection.style.display = 'block';
    elements.searchBtn.disabled = true;
    elements.searchBtn.textContent = '搜索中...';
    
    // 更新状态消息(显示搜索范围)
    elements.statusMessage.textContent = `正在搜索: ${startSeed.toLocaleString()}-${endSeed.toLocaleString()}`;

    elements.statusMessage.className = 'status-message status-searching';
    elements.progressBar.style.width = '0%';
    elements.progressBar.textContent = '0%';

    elements.checkedCount.textContent = '0';
    elements.foundCount.textContent = '0';
    elements.speed.textContent = '0';
    elements.elapsed.textContent = '0.0s';

    // 发送搜索请求
    try {
        const weatherConditions = weatherEnabled ? conditions.map(c => ({
            season: c.season,
            startDay: c.startDay,
            endDay: c.endDay,
            minRainDays: c.minRain
        })) : [];

        const fairyConditionsData = fairyEnabled ? fairyConditions.filter(c => c).map(c => ({
            year: c.year,
            season: c.season,
            day: c.day
        })) : [];

        const mineChestConditionsData = mineChestEnabled ? mineChestConditions.filter(c => c).map(c => ({
            Floor: c.Floor,
            ItemName: c.ItemName
        })) : [];

        const monsterLevelConditionsData = monsterLevelEnabled ? monsterLevelConditions.filter(c => c).map(c => ({
            startDay: c.startDay,
            endDay: c.endDay,
            startLevel: c.startLevel,
            endLevel: c.endLevel
        })) : [];

        const cartConditionsData = cartEnabled ? validCartConditions.map(c => ({
            startYear: c.startYear,
            startSeason: { '春': 0, '夏': 1, '秋': 2, '冬': 3 }[c.startSeason], // 转换为 0-3
            startDay: c.startDay,
            endYear: c.endYear,
            endSeason: { '春': 0, '夏': 1, '秋': 2, '冬': 3 }[c.endSeason],     // 转换为 0-3
            endDay: c.endDay,
            itemName: c.itemName,
            requireQty5: c.requireQty5
        })) : [];
        
        console.log('怪物层条件:', monsterLevelConditionsData);

        const response = await fetch('http://localhost:5000/api/search', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                startSeed,
                endSeed,
                useLegacyRandom: useLegacy,
                weatherConditions,
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
    }
});

/**
 * 将绝对天数转换为 [季节][天数] 格式
 */
function convertAbsoluteDay(day) {
    const seasonNames = ['春', '夏', '秋', '冬'];
    const daysPerSeason = 28;

    // 绝对天数从 1 开始
    const adjustedDay = day - 1; 
    
    const seasonIndex = Math.floor(adjustedDay / daysPerSeason);
    const dayOfMonth = (adjustedDay % daysPerSeason) + 1;
    
    // 确保季节在范围内
    if (seasonIndex >= 0 && seasonIndex < seasonNames.length) {
        return `${seasonNames[seasonIndex]}${dayOfMonth}`;
    }
    return `第${day}天`; 
}

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
        const seasonMap = { Spring: '春', Summer: '夏', Fall: '秋' };
        const fairyText = details.fairy.days.map(f => {
            const prefix = f.year === 1 ? '' : `${f.year}年`;
            return `${prefix}${seasonMap[f.season]}${f.day}`;
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

connectWebSocket();