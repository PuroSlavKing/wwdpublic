### UI

chat-manager-max-message-length = Your message exceeds {$maxMessageLength} character limit
chat-manager-ooc-chat-enabled-message = OOC chat has been enabled.
chat-manager-ooc-chat-disabled-message = OOC chat has been disabled.
chat-manager-looc-chat-enabled-message = LOOC chat has been enabled.
chat-manager-looc-chat-disabled-message = LOOC chat has been disabled.
chat-manager-dead-looc-chat-enabled-message = Dead players can now use LOOC.
chat-manager-dead-looc-chat-disabled-message = Dead players can no longer use LOOC.
chat-manager-crit-looc-chat-enabled-message = Crit players can now use LOOC.
chat-manager-crit-looc-chat-disabled-message = Crit players can no longer use LOOC.
chat-manager-admin-ooc-chat-enabled-message = Admin OOC chat has been enabled.
chat-manager-admin-ooc-chat-disabled-message = Admin OOC chat has been disabled.

chat-manager-max-message-length-exceeded-message = Your message exceeded {$limit} character limit
chat-manager-no-headset-on-message = You don't have a headset on!
chat-manager-no-radio-key = No radio key specified!
chat-manager-no-such-channel = There is no channel with key '{$key}'!
chat-manager-whisper-headset-on-message = You can't whisper on the radio!

chat-manager-language-prefix = ({ $language }){" "}

chat-manager-server-wrap-message = [bold]{$message}[/bold]
chat-manager-sender-announcement = Central Command
chat-manager-sender-announcement-wrap-message = [font size=14][bold]{$sender} Announcement:[/font][font size=12]
                                                {$message}[/bold][/font]

# For the message in double quotes, the font/color/bold/italic elements are repeated twice, outside the double quotes and inside.
# The outside elements are for formatting the double quotes, and the inside elements are for formatting the text in speech bubbles ([BubbleContent]).
chat-manager-entity-say-wrap-message = [BubbleHeader][Name][font size=11][color={$color}][bold]{$language}[/bold][/color][/font][bold]{$entityName}[/bold][/Name][/BubbleHeader] {$verb}, [font="{$fontType}" size={$fontSize} ][color={$color}]"[BubbleContent][font="{$fontType}" size={$fontSize}][color={$color}]{$message}[/color][/font][/BubbleContent]"[/color][/font]
chat-manager-entity-say-bold-wrap-message = [BubbleHeader][Name][font size=11][color={$color}][bold]{$language}[/bold][/color][/font][bold]{$entityName}[/bold][/Name][/BubbleHeader] {$verb}, [font="{$fontType}" size={$fontSize} ][color={$color}][bold]"[BubbleContent][font="{$fontType}" size={$fontSize}][color={$color}][bold]{$message}[/bold][/color][/font][/BubbleContent]"[/bold][/color][/font]

chat-manager-entity-whisper-wrap-message = [BubbleHeader][Name][font size=10][color={$color}][bold]{$language}[/bold][/color][/font][font size=11][italic]{$entityName}[/Name][/BubbleHeader] whispers, [font="{$fontType}"][color={$color}][italic]"[BubbleContent][font="{$fontType}"][color={$color}][italic]{$message}[/italic][/color][/font][/BubbleContent]"[/italic][/color][/font][/italic][/font]
chat-manager-entity-whisper-unknown-wrap-message = [BubbleHeader][font size=10][color={$color}][bold]{$language}[/bold][/color][/font][font size=11][italic]Someone[/BubbleHeader] whispers, [font="{$fontType}"][color={$color}][italic]"[BubbleContent][font="{$fontType}"][color={$color}][italic]{$message}[/italic][/color][/font][/BubbleContent]"[/italic][/color][/font][/italic][/font]

# THE() is not used here because the entity and its name can technically be disconnected if a nameOverride is passed...
chat-manager-entity-me-wrap-message = [italic]{ PROPER($entity) ->
    *[false] The {$entityName} {$message}[/italic]
     [true] {CAPITALIZE($entityName)} {$message}[/italic]
    }

chat-manager-entity-looc-wrap-message = LOOC: {$entityName}: {$message}
chat-manager-send-ooc-wrap-message = OOC: {$playerName}: {$message}
chat-manager-send-ooc-patron-wrap-message = OOC: [color={$patronColor}]{$playerName}[/color]: {$message}

chat-manager-send-dead-chat-wrap-message = {$deadChannelName}: [BubbleHeader]{$playerName}[/BubbleHeader]: [BubbleContent]{$message}[/BubbleContent]
chat-manager-send-admin-dead-chat-wrap-message = {$adminChannelName}: ([BubbleHeader]{$userName}[/BubbleHeader]): [BubbleContent]{$message}[/BubbleContent]
chat-manager-send-admin-chat-wrap-message = {$adminChannelName}: {$playerName}: {$message}
chat-manager-send-admin-announcement-wrap-message = [bold]{$adminChannelName}: {$message}[/bold]

chat-manager-send-hook-ooc-wrap-message = OOC: (D){$senderName}: {$message}

chat-manager-dead-channel-name = DEAD
chat-manager-admin-channel-name = ADMIN

chat-manager-rate-limited = You are sending messages too quickly!
chat-manager-rate-limit-admin-announcement = Player { $player } breached chat rate limits. Watch them if this is a regular occurence.

chat-manager-send-empathy-chat-wrap-message = {$source}: {$message}

chat-manager-send-cult-chat-wrap-message = [bold]\[{ $channelName }\] [BubbleHeader]{ $player }[/BubbleHeader]:[/bold] [BubbleContent]{ $message }[/BubbleContent]
chat-manager-cult-channel-name = Blood Cult

## Speech verbs for chat

chat-speech-verb-suffix-exclamation = !
chat-speech-verb-suffix-exclamation-strong = !!
chat-speech-verb-suffix-question = ?
chat-speech-verb-suffix-stutter = -
chat-speech-verb-suffix-mumble = ..

chat-speech-verb-name-none = None
chat-speech-verb-name-default = Default
chat-speech-verb-default = says
chat-speech-verb-name-exclamation = Exclaiming
chat-speech-verb-exclamation = exclaims
chat-speech-verb-name-exclamation-strong = Yelling
chat-speech-verb-exclamation-strong = yells
chat-speech-verb-name-question = Asking
chat-speech-verb-question = asks
chat-speech-verb-name-stutter = Stuttering
chat-speech-verb-stutter = stutters
chat-speech-verb-name-mumble = Mumbling
chat-speech-verb-mumble = mumbles

chat-speech-verb-name-arachnid = Arachnid
chat-speech-verb-insect-1 = chitters
chat-speech-verb-insect-2 = chirps
chat-speech-verb-insect-3 = clicks

chat-speech-verb-name-moth = Moth
chat-speech-verb-winged-1 = flutters
chat-speech-verb-winged-2 = flaps
chat-speech-verb-winged-3 = buzzes

chat-speech-verb-name-slime = Slime
chat-speech-verb-slime-1 = sloshes
chat-speech-verb-slime-2 = burbles
chat-speech-verb-slime-3 = oozes

chat-speech-verb-name-plant = Diona
chat-speech-verb-plant-1 = rustles
chat-speech-verb-plant-2 = sways
chat-speech-verb-plant-3 = creaks

chat-speech-verb-name-robotic = Robotic
chat-speech-verb-robotic-1 = states
chat-speech-verb-robotic-2 = beeps
chat-speech-verb-robotic-3 = boops

chat-speech-verb-name-reptilian = Reptilian
chat-speech-verb-reptilian-1 = hisses
chat-speech-verb-reptilian-2 = snorts
chat-speech-verb-reptilian-3 = huffs

chat-speech-verb-name-skeleton = Skeleton / Plasmaman
chat-speech-verb-skeleton-1 = rattles
chat-speech-verb-skeleton-2 = ribs
chat-speech-verb-skeleton-3 = bones
chat-speech-verb-skeleton-4 = clacks
chat-speech-verb-skeleton-5 = cracks

chat-speech-verb-name-vox = Vox
chat-speech-verb-vox-1 = screeches
chat-speech-verb-vox-2 = shrieks
chat-speech-verb-vox-3 = croaks

chat-speech-verb-name-oni = Oni
chat-speech-verb-oni-1 = grunts
chat-speech-verb-oni-2 = bellows
chat-speech-verb-oni-3 = blares
chat-speech-verb-oni-4 = rumbles

chat-speech-verb-name-canine = Canine
chat-speech-verb-canine-1 = barks
chat-speech-verb-canine-2 = woofs
chat-speech-verb-canine-3 = howls

chat-speech-verb-name-small-mob = Mouse
chat-speech-verb-small-mob-1 = squeaks
chat-speech-verb-small-mob-2 = pieps

chat-speech-verb-name-large-mob = Carp
chat-speech-verb-large-mob-1 = roars
chat-speech-verb-large-mob-2 = growls

chat-speech-verb-name-monkey = Monkey
chat-speech-verb-monkey-1 = chimpers
chat-speech-verb-monkey-2 = screeches

chat-speech-verb-name-cluwne = Cluwne

chat-speech-verb-name-parrot = Parrot
chat-speech-verb-parrot-1 = squawks
chat-speech-verb-parrot-2 = tweets
chat-speech-verb-parrot-3 = chirps

chat-speech-verb-cluwne-1 = giggles
chat-speech-verb-cluwne-2 = guffaws
chat-speech-verb-cluwne-3 = laughs

chat-speech-verb-name-ghost = Ghost
chat-speech-verb-ghost-1 = complains
chat-speech-verb-ghost-2 = breathes
chat-speech-verb-ghost-3 = hums
chat-speech-verb-ghost-4 = mutters

chat-speech-verb-name-electricity = Electricity
chat-speech-verb-electricity-1 = crackles
chat-speech-verb-electricity-2 = buzzes
chat-speech-verb-electricity-3 = screeches

chat-speech-verb-marish = Mars

chat-speech-verb-name-supermatter = Supermatter
chat-speech-verb-supermatter = states


chat-speech-verb-Psychomantic-1 = resonates
chat-speech-verb-Psychomantic-2 = projects
chat-speech-verb-Psychomantic-3 = impresses
chat-speech-verb-Psychomantic-4 = radiates
