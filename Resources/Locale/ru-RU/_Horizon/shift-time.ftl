shift-time-with-days = { $days } { $days ->
    [one] день
    [few] дня
   *[other] дней
} { $time }

lobby-state-player-status-round-time-days =
    Время раунда: { $days } { $days ->
        [one] день
        [few] дня
       *[other] дней
    } { $hours } { $hours ->
        [one] час
        [few] часа
       *[other] часов
    } и { $minutes } { $minutes ->
        [one] минута
        [few] минуты
       *[other] минут
    }

round-end-summary-window-duration-label-days = Он длился [color=yellow]{ $days } { $days ->
    [one] день
    [few] дня
   *[other] дней
}, { $hours } ч., { $minutes } мин., и { $seconds } сек.
