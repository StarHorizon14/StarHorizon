shift-time-with-days = { $days } { $days ->
    [one] day
   *[other] days
} { $time }

lobby-state-player-status-round-time-days =
    The round time is: { $days } { $days ->
        [one] day
       *[other] days
    }, { $hours } { $hours ->
        [one] hour
       *[other] hours
    } and { $minutes } { $minutes ->
        [one] minute
       *[other] minutes
    }

round-end-summary-window-duration-label-days = It lasted for [color=yellow]{ $days } { $days ->
    [one] day
   *[other] days
}, { $hours } hours, { $minutes } minutes, and { $seconds } seconds.
