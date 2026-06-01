const connection = new signalR.HubConnectionBuilder()
   .withUrl("/matchhub")
   .configureLogging(signalR.LogLevel.Information)
   .build();

const logBox = document.getElementById("log");
const boardElement = document.getElementById("board");

// ==========================================
// ГЛОБАЛЬНОЕ СОСТОЯНИЕ (DATA LAYER)
// ==========================================
let selectedCell = null; 
let validMoves = []; 
let currentGridCopy = null;       // Кэш последней прилетевшей матрицы игрового поля
let isBoardReversed = false;      // Флаг разворота (false = белые снизу, true = черные снизу)
let clientColor = "White";        // Цвет текущего игрока в сессии

function log(message) {
    logBox.innerHTML += `<div>[${new Date().toLocaleTimeString()}] ${message}</div>`;
    logBox.scrollTop = logBox.scrollHeight;
}

// === УТИЛИТЫ КООРДИНАТ И ШАХМАТНОЙ НОТАЦИИ ===
function toChessNotation(row, col) {
    // 65 — это ASCII код большой латинской буквы 'A' (65='A', 66='B', 67='C' и т.д.)
    // String.fromCharCode динамически вычисляет букву для любой ширины доски без массивов
    const letter = String.fromCharCode(65 + col);
    const chessRow = row + 1; 
    return `${letter}${chessRow}`;
}

// ==========================================
// СЕТЕВОЙ СЛОЙ (ПОДПИСКИ НА СОБЫТИЯ SIGNALR)
// ==========================================

/**
 * Событие: Комната успешно зарегистрирована на сервере.
 * Вызывается после создания сессии, возвращает GUID комнаты.
 */
connection.on("RoomCreated", (matchId) => {
    log(`Комната зафиксирована в сети! ID: <b>${matchId}</b>. Ожидаем оппонента...`);
    document.getElementById("roomInput").value = matchId;
});

/**
 * Событие: Оба игрока подключились, матч начинается.
 * @param {string} assignedColor - Цвет, назначенный этому клиенту ("White" или "Black")
 * @param {object} sessionInfo - Стартовое состояние игры и доски от C# ядра
 */
connection.on("GameStarted", (assignedColor, sessionInfo) => {
    clientColor = assignedColor;
    document.getElementById("playerColor").innerText = assignedColor === "White" ? "Белые" : "Черные";
    document.getElementById("gameStatus").innerText = "Игра идет!";
    log(`Игра началась! Вы играете за: ${assignedColor === "White" ? "Белых" : "Черных"}`);

    isBoardReversed = (clientColor === "Black");
    currentGridCopy = sessionInfo.grid || sessionInfo.Grid;
    drawBoard(currentGridCopy, isBoardReversed);
});

/**
 * Событие: Очередь хода данного клиента.
 * @param {Array} moves - Список валидных объектов Move, сгенерированных ядерным движком
 */
connection.on("YourTurn", (moves) => {
    validMoves = moves; 
    document.getElementById("gameStatus").innerText = "Ваш ход!";
    log(`Ваш ход! Доступно вариантов: ${moves.length}`);
});

/**
 * Событие: Доска изменилась после чьего-то хода.
 * @param {object} sessionInfo - Актуальное состояние сессии для перерисовки поля
 */
connection.on("UpdateGameState", (sessionInfo) => {
    validMoves = []; 
    
    const activeSide = sessionInfo.activeSide !== undefined ? sessionInfo.activeSide : sessionInfo.ActiveSide;
    const sideText = (activeSide === "White" || activeSide === 0) ? "Белых" : "Черных";
    
    document.getElementById("gameStatus").innerText = `Ход ${sideText}...`;

    currentGridCopy = sessionInfo.grid || sessionInfo.Grid;
    drawBoard(currentGridCopy, isBoardReversed);
});

/**
 * Событие: На сервере произошла ошибка (невалидный ход, занятое место).
 */
connection.on("Error", (message) => {
    log(`<span style="color:red">Ошибка бэкенда: ${message}</span>`);
    resetSelection();
});

/**
 * Событие: Финал игры (матч завершен нормально или прерван дисконнектом).
 */
connection.on("GameOver", (endMessage) => {
    validMoves = [];
    document.getElementById("gameStatus").innerText = "МАТЧ ЗАВЕРШЕН";
    log(`<b style="color: green; font-size: 14px;">${endMessage}</b>`);
    selectedCell = null;
    
    setTimeout(() => {
        alert(endMessage);
    }, 500);
});

// Запуск WebSocket соединения
connection.start()
    .then(() => log("Успешно подключено к SignalR бэкенду!"))
    .catch(err => log(`Ошибка подключения: ${err.toString()}`));

// ==========================================
// ИНТЕРФЕЙС УПРАВЛЕНИЯ КОМНАТАМИ
// ==========================================

// Инициализация матча через HTTP POST с передачей настроек DTO
document.getElementById("btnCreate").addEventListener("click", () => {

    const selectedVariant = document.getElementById("selectVariant").value;
    const matchSettings = {
        variant: selectedVariant
    };
    fetch('/api/matchmaker/create', { 
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(matchSettings)
    })
        .then(response => {
            if (!response.ok) throw new Error("Ошибка сервера при создании матча");
            return response.json();
        })
        .then(data => {
            const finalMatchId = data.matchId || data.MatchId;

            log(`Комната успешно создана по HTTP! Режим: <b>${selectedVariant}</b>. ID: <b>${finalMatchId}</b>.`);
            document.getElementById("roomInput").value = finalMatchId;
            
            // Занимаем место Создателя (Белых) через сокеты
            connection.invoke("JoinRoom", finalMatchId).catch(err => console.error(err));
        })
        .catch(err => log(`<span style="color:red">Ошибка матчмейкера: ${err.message}</span>`));
});

document.getElementById("btnJoin").addEventListener("click", () => {
    const roomId = document.getElementById("roomInput").value.trim();
    if (roomId) {
        connection.invoke("JoinRoom", roomId).catch(err => console.error(err));
    }
});

// ==========================================
// МОДУЛЬ ОТРИСОВКИ ДОСКИ (PRESENTATION LAYER)
// ==========================================
function drawBoard(grid, reverseView) {
    boardElement.innerHTML = "";

    const totalRows = grid.length;
    const maxCol = grid.reduce((max, row) => Math.max(max, row ? row.length : 0), 0);

    boardElement.style.gridTemplateColumns = `repeat(${maxCol}, 1fr)`;
    boardElement.style.gridTemplateRows = `repeat(${totalRows}, 1fr)`;

    let rowIndices = Array.from({ length: totalRows }, (_, i) => totalRows - 1 - i);
    let colIndices = Array.from({ length: maxCol }, (_, i) => i);

    if (reverseView) {
        rowIndices = Array.from({ length: totalRows }, (_, i) => i); 
        colIndices = Array.from({ length: maxCol }, (_, i) => maxCol - 1 - i); 
    }

    rowIndices.forEach(row => {
        colIndices.forEach(col => {
            // Защита: если ячейка физически отсутствует в рваной строке — считаем её пустой клетки (null)
            const piece = (grid[row] && grid[row][col] !== undefined) ? grid[row][col] : null;
            const cell = document.createElement("button");
            
            cell.className = `cell ${(row + col) % 2 === 0 ? "black-cell" : "white-cell"}`;
            cell.setAttribute("data-coord", `${row}_${col}`);

            let cellText = toChessNotation(row, col);

            if (piece !== null && piece !== undefined) {
                const side = piece.Side !== undefined ? piece.Side : piece.side;
                const type = piece.Type !== undefined ? piece.Type : piece.type;

                if (side === "White" || side === 0) {
                    cellText += (type === "King" || type === 1) ? " 👑" : " ⚪";
                } else if (side === "Black" || side === 1) {
                    cellText += (type === "King" || type === 1) ? " ⭐" : " ⚫";
                }
            }

            cell.innerText = cellText;
            cell.addEventListener("click", () => handleCellClick(row, col, cell));
            boardElement.appendChild(cell);
        });
    });
}


// ==========================================
// ЛОГИКА ВЗАИМОДЕЙСТВИЯ КЛИКОВ (CONTROLLER LAYER)
// ==========================================
function handleCellClick(row, col, cellElement) {
    // ШАГ А: Игрок выбирает НАЧАЛЬНУЮ клетку (свою шашку)
    if (!selectedCell) {
        selectedCell = { row: row, col: col };
        cellElement.classList.add("selected-cell");
        log(`Выбрана клетка: ${toChessNotation(row, col)} [${row}, ${col}]`);

        // Вытаскиваем координаты с защитой от регистра C#
        const possibleMovesForThisPiece = validMoves.filter(move => {
            const from = move.from || move.From;
            const fromRow = from.row !== undefined ? from.row : from.Row;
            const fromCol = from.col !== undefined ? from.col : from.Col;
            return fromRow === row && fromCol === col;
        });

        possibleMovesForThisPiece.forEach(move => {
            const to = move.to || move.To;
            const toRow = to.row !== undefined ? to.row : to.Row;
            const toCol = to.col !== undefined ? to.col : to.Col;
            
            const targetCellButton = document.querySelector(`[data-coord="${toRow}_${toCol}"]`);
            if (targetCellButton) {
                targetCellButton.classList.add("valid-target-cell");
            }
        });

        if (possibleMovesForThisPiece.length === 0) {
            log(`<span style="color:orange">У этой фигуры нет доступных ходов!</span>`);
        }
    } 
    // ШАГ Б: Игрок кликает по ЦЕЛЕВОЙ клетке (совершает перемещение)
    else {
        // Красивый, вычищенный поиск совпадения хода без забора из тернаров
        const matchedMove = validMoves.find(move => {
            const from = move.from || move.From;
            const to = move.to || move.To;
            
            const fromRow = from.row !== undefined ? from.row : from.Row;
            const fromCol = from.col !== undefined ? from.col : from.Col;
            const toRow = to.row !== undefined ? to.row : to.Row;
            const toCol = to.col !== undefined ? to.col : to.Col;

            return fromRow === selectedCell.row && fromCol === selectedCell.col && toRow === row && toCol === col;
        });

        if (matchedMove) {
            log(`Отправка хода: из ${toChessNotation(selectedCell.row, selectedCell.col)} в ${toChessNotation(row, col)}`);
            
            // Отправляем объект Move целиком
            connection.invoke("SendMove", matchedMove)
                .catch(err => console.error(err));
        } else {
            log(`<span style="color:orange">Ход заблокирован: нелегальная траектория</span>`);
        }
        
        resetSelection();
    }
}

function resetSelection() {
    document.querySelectorAll('.cell').forEach(cell => {
        cell.classList.remove("selected-cell");
        cell.classList.remove("valid-target-cell");
    });
    selectedCell = null;
}
