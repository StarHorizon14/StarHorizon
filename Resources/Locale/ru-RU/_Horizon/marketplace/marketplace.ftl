marketplace-item-slot-name = Лоток пункта выдачи

## Окно

marketplace-window-title = Пункт выдачи StarOZON
marketplace-header-title = [font size=16][bold][color=#7CC4FF]Star[/color][color=#E8EEF7]OZON[/color][/bold][/font]  [color=#8391A5]· пункт выдачи · ВОЗРАТУ НЕ ПОДЛЕЖИТ[/color]
marketplace-header-terminal = [color={$mutedColor}]Терминал:[/color] [color={$accentColor}]{$grid}[/color]

marketplace-stat-balance = [color={$mutedColor}]Баланс[/color]  [bold][color={$priceColor}]{$balance}[/color][/bold]
marketplace-stat-no-account = [color=#FF6B6B]Нет банковского счёта[/color]
marketplace-stat-tax = [color={$mutedColor}]Налог[/color]  [bold]{$tax}%[/bold]
marketplace-stat-listings = [color={$mutedColor}]Лотов[/color]  [bold]{$count}[/bold]
marketplace-stat-pending = [color={$mutedColor}]Ожидает выплаты[/color]  [bold][color=#FFD866]{$amount}[/color][/bold]

marketplace-tab-market = Рынок
marketplace-tab-sell = Продать
marketplace-tab-mine = Мои лоты

marketplace-search-placeholder = Поиск: предмет, продавец, станция или #номер лота
marketplace-sort-label = Сортировка:
marketplace-sort-newest = Сначала новые
marketplace-sort-price-asc = Цена: по возрастанию
marketplace-sort-price-desc = Цена: по убыванию
marketplace-sort-name = По названию
marketplace-sort-count = По количеству
marketplace-empty = На рынке пока нет ни одного лота.
marketplace-empty-search = Ничего не найдено.
marketplace-mine-empty = У вас нет выставленных лотов.

marketplace-examine-hint = Shift+ЛКМ — осмотреть предмет

## Карточка лота

marketplace-card-price-unit = [bold][color={$priceColor}]{$price}[/color][/bold] [color={$mutedColor}]за шт.[/color]
marketplace-card-price-single = [bold][color={$priceColor}]{$price}[/color][/bold]
marketplace-card-stock = [color={$mutedColor}]В наличии:[/color] {$count} шт.
marketplace-card-seller = [color={$mutedColor}]Продавец:[/color] {$seller}
marketplace-card-id = [color={$mutedColor}]ID-карта:[/color] {$idName}
marketplace-card-station = [color={$mutedColor}]Станция:[/color] [color={$accentColor}]{$grid}[/color]  [color={$mutedColor}]· Лот #{$id}[/color]
marketplace-card-total = [color={$mutedColor}]Итого:[/color] [bold][color={$color}]{$total}[/color][/bold]
marketplace-card-own = [color={$accentColor}]Ваш лот[/color]
marketplace-card-amount = Кол-во:
marketplace-card-buy = Купить
marketplace-card-withdraw = Снять

## Продажа

marketplace-sell-slot-title = Предмет в терминале
marketplace-sell-slot-empty = [color={$mutedColor}]Лоток пуст[/color]
marketplace-sell-available = [color={$mutedColor}]В наличии:[/color] {$count} шт.
marketplace-sell-eject = Извлечь предмет
marketplace-sell-title = Новый лот
marketplace-sell-amount = Количество
marketplace-sell-all = Всё
marketplace-sell-price-unit = Цена за штуку
marketplace-sell-price-single = Цена
marketplace-sell-sum = Сумма продажи
marketplace-sell-tax = Налог ({$tax}%)
marketplace-sell-receive = Вы получите
marketplace-sell-merge = [color={$accentColor}]Будет объединено с вашим лотом #{$id} (сейчас {$count} шт.) — та же цена и тот же ресурс.[/color]
marketplace-sell-button = Выставить на рынок

## Сообщения

marketplace-no-id = Без ID-карты
marketplace-unknown-grid = Неизвестная станция

marketplace-error-no-item = В лотке нет предмета.
marketplace-error-no-account = У вас нет банковского счёта.
marketplace-error-bad-price = Цена должна быть от 1 до {$max}.
marketplace-error-forbidden = Этот предмет нельзя выставить на продажу.
marketplace-error-too-many-listings = Нельзя иметь больше {$max} лотов одновременно.
marketplace-error-failed = Не удалось выставить предмет.
marketplace-error-no-listing = Этот лот больше недоступен.
marketplace-error-not-owner = Это не ваш лот.
marketplace-error-price-changed = Цена лота изменилась, проверьте ещё раз.
marketplace-error-own-listing = Нельзя купить собственный лот.
marketplace-error-bad-amount = Можно купить от 1 до {$max} шт.
marketplace-error-too-expensive = Слишком крупная сделка, уменьшите количество.
marketplace-error-insufficient-funds = Недостаточно средств: нужно {$amount}.

marketplace-popup-listed = {$item} выставлен на рынок (лот #{$id}).
marketplace-popup-merged = {$item} добавлен к вашему лоту #{$id}.
marketplace-popup-withdrawn = Лот #{$id} снят с продажи.
marketplace-popup-bought = Куплено: {$item} ×{$amount} за {$total}.
marketplace-popup-sold = Продано: {$item} ×{$amount}. Зачислено {$payout} (налог {$tax}).
marketplace-popup-sold-pending = Продано: {$item} ×{$amount}. {$payout} ожидает выплаты — заберите в пункте выдачи или терминале.
marketplace-popup-pending-paid = Зачислена выручка с продаж: {$amount}.

## Прототипы

ent-ComputerMarketplace = пункт выдачи StarOZON
    .desc = Терминал маркетплейса StarOZON: выставляйте товары, покупайте у других космонавтов и забирайте заказы. ВОЗРАТУ НЕ ПОДЛЕЖИТ.
ent-MarketplaceComputerCircuitboard = плата пункта выдачи StarOZON
    .desc = Печатная плата для пункта выдачи StarOZON.

## Запросы

marketplace-tab-requests = Запросы
marketplace-request-search-placeholder = Поиск: что ищут, заказчик, станция или #номер запроса
marketplace-request-only-mine = Только мои
marketplace-request-empty = Пока никто ничего не ищет. Разместите первый запрос!

marketplace-request-create-title = Новый запрос
marketplace-request-field-title = Что нужно
marketplace-request-field-title-placeholder = Например: пласталь для обшивки
marketplace-request-field-description = Описание
marketplace-request-field-quantity = Количество
marketplace-request-field-reward = Награда за весь заказ
marketplace-request-summary-held = [color={$mutedColor}]Будет заморожено на счёте:[/color] [bold]{$reward}[/bold]
marketplace-request-summary-payout = [color={$mutedColor}]Исполнитель получит:[/color] [bold][color={$priceColor}]{$payout}[/color][/bold] [color={$mutedColor}]({$share}%)[/color]
marketplace-request-create-button = Разместить запрос

marketplace-request-card-meta = [color={$mutedColor}]Нужно:[/color] {$quantity} шт.  [color={$mutedColor}]· Предложений:[/color] {$offers}  [color={$mutedColor}]· Запрос #{$id}[/color]
marketplace-request-card-reward = [color={$mutedColor}]награда[/color]
marketplace-request-card-requester = [color={$mutedColor}]Заказчик:[/color] {$name}  [color={$mutedColor}]· ID:[/color] {$idName}  [color={$mutedColor}]· Станция:[/color] [color={$accentColor}]{$grid}[/color]
marketplace-request-card-no-offers = [color={$mutedColor}]Ваш запрос. Предложений пока нет.[/color]
marketplace-request-card-offers-header = [color={$accentColor}]Ваш запрос.[/color] Предложения ({$count}) — примите подходящее:
marketplace-request-cancel = Отменить запрос

marketplace-offer-button = Предложить
marketplace-offer-accept = Принять
marketplace-offer-reject = Отклонить
marketplace-offer-withdraw = Отозвать
marketplace-offer-take-back = Забрать
marketplace-offer-hint-empty = [color={$mutedColor}]Положите предмет в лоток консоли, чтобы предложить его.[/color]
marketplace-offer-hint-item = [color={$mutedColor}]В лотке:[/color] [color={$accentColor}]{$item}[/color]
marketplace-offer-hint-already = [color={$accentColor}]Вы уже сделали предложение по этому запросу.[/color]
marketplace-offer-row-offerer = [color={$mutedColor}]От:[/color] {$name}  [color={$mutedColor}]· ID:[/color] {$idName}  [color={$mutedColor}]· Станция:[/color] [color={$accentColor}]{$grid}[/color]
marketplace-offer-row-request = [color={$mutedColor}]К запросу:[/color] {$title}  [color={$mutedColor}]· награда[/color] [color={$priceColor}]{$reward}[/color]
marketplace-offer-status-pending = [color=#FFD866]Ожидает решения заказчика[/color]
marketplace-offer-status-returned = [color=#FF8A6B]Отклонено — заберите предмет[/color]

marketplace-my-offers-title = Мои предложения
marketplace-my-offers-empty = Вы ещё ничего не предлагали.

marketplace-request-error-title = Название запроса должно быть от 1 до {$max} символов.
marketplace-request-error-quantity = Количество должно быть от 1 до {$max}.
marketplace-request-error-too-many = Нельзя иметь больше {$max} открытых запросов.
marketplace-request-error-missing = Этот запрос больше не существует.
marketplace-request-error-not-owner = Это не ваш запрос.
marketplace-request-error-own = Нельзя предложить предмет на собственный запрос.
marketplace-request-error-too-many-offers = У этого запроса уже {$max} предложений.
marketplace-request-error-too-many-own-offers = Нельзя иметь больше {$max} предложений одновременно.
marketplace-offer-error-missing = Это предложение больше недоступно.
marketplace-offer-error-not-owner = Это не ваше предложение.

marketplace-request-popup-created = Запрос «{$title}» размещён, {$reward} заморожено.
marketplace-request-popup-cancelled = Запрос «{$title}» отменён, {$reward} возвращено.
marketplace-offer-popup-made = {$item} предложен на запрос «{$title}».
marketplace-offer-popup-received = Новое предложение на ваш запрос «{$title}»: {$item}.
marketplace-offer-popup-accepted = Сделка состоялась: вы получили {$item} по запросу «{$title}».
marketplace-offer-popup-paid = Ваше предложение по запросу «{$title}» принято! Зачислено {$payout} (налог {$tax}).
marketplace-offer-popup-paid-pending = Ваше предложение по запросу «{$title}» принято! {$payout} ожидает выплаты — заберите в пункте выдачи или терминале.
marketplace-offer-popup-rejected = Предложение «{$item}» отклонено.
marketplace-offer-popup-withdrawn = {$item} возвращён вам.

marketplace-error-contraband = Контрабанду 2-го класса и выше нельзя продавать в StarOZON.
marketplace-error-contraband-inside = Внутри предмета контрабанда 2-го класса или выше — выньте её, прежде чем продавать.

## Чёрный рынок

marketplace-black-window-title = StarBerries
marketplace-black-header-title = [font size=16][bold][color=#FF4D57]Star[/color][color=#F2E4E6]Berries[/color][/bold][/font]  [color=#A38A8E]· тёмная сторона Star маркета[/color]
marketplace-anon-name = Аноним {$code}
marketplace-anon-id = [зашифровано]
marketplace-anon-grid = Шифр {$code}

ent-ComputerMarketplaceBlackMarket = терминал StarBerries
    .desc = Зашифрованный терминал StarBerries. Ни имён, ни вопросов, ни возврата — только товар и кредиты.
ent-MarketplaceBlackMarketComputerCircuitboard = плата терминала StarBerries
    .desc = Печатная плата терминала StarBerries. Маркировка старательно счищена.

## Программы КПК

marketplace-pda-program-name = StarOZON
marketplace-black-pda-program-name = StarBerries
marketplace-pda-tab-new-request = Новый запрос
marketplace-pda-subtitle = [color={$mutedColor}]Налог[/color] {$tax}%  [color={$mutedColor}]· Лотов[/color] {$listings}  [color={$mutedColor}]· Запросов[/color] {$requests}  [color={$mutedColor}]· Баланс[/color] [color={$priceColor}]{$balance}[/color]
marketplace-pda-request-note = [color={$mutedColor}]Награда списывается сразу. Покупки, предложения и их принятие — только в пункте выдачи или терминале.[/color]
marketplace-card-total-all = [color={$mutedColor}]За всё:[/color] [bold][color={$priceColor}]{$total}[/color][/bold]  [color={$mutedColor}]· купить в пункте выдачи[/color]
marketplace-request-card-own-pda = [color={$accentColor}]Ваш запрос.[/color] Предложений: {$count} [color={$mutedColor}]— примите здесь с доставкой телепортом или в пункте выдачи.[/color]
marketplace-request-card-offer-pda = [color={$mutedColor}]Предложить предмет можно в пункте выдачи.[/color]

ent-MarketplaceViewerCartridge = картридж «StarOZON»
    .desc = Приложение StarOZON для КПК: смотрите товары и оставляйте заявки, не вставая с кресла пилота.
ent-BlackMarketViewerCartridge = картридж «StarBerries»
    .desc = Картридж без маркировки с зашифрованным подключением к StarBerries.
marketplace-examine-more = ПКМ или Shift+ЛКМ осмотреть предмет

## Доставка телепортом

marketplace-crystal-slot-name = Блюспейс-кристаллы
marketplace-delivery-accept = Принять и доставить
marketplace-delivery-title = [bold]Доставка через секторный телепорт[/bold]
marketplace-delivery-item = [color={$mutedColor}]Посылка:[/color] {$item} ×{$count}
marketplace-delivery-crystals = [color={$mutedColor}]Блюспейс-кристаллов в КПК:[/color] [color={$accentColor}]{$count}[/color] [color={$mutedColor}]· за доставку спишется {$cost}[/color]
marketplace-delivery-crystals-missing = [color=#FF6B6B]Нужен {$cost} блюспейс-кристалл в КПК (сейчас {$count}).[/color] [color={$mutedColor}]Вставьте кристаллы в КПК.[/color]
marketplace-delivery-no-teleporters = В секторе нет секторных телепортов.
marketplace-delivery-unpowered = нет питания
marketplace-delivery-send = Доставить сюда
marketplace-delivery-cancel = Отмена
marketplace-delivery-error-teleporter = Этот телепорт недоступен.
marketplace-delivery-error-unpowered = У телепорта нет питания.
marketplace-delivery-error-crystals = Для доставки нужен {$cost} блюспейс-кристалл в КПК.
marketplace-delivery-popup-sent = {$item} отправлен через телепорт: {$destination}.
