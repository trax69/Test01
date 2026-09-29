# Levanta rabbitA y rabbitB, habilita el plugin Shovel y las conecta a una red Docker compartida.

$ErrorActionPreference = "Stop"

function New-RabbitContainer {
    param(
        [string]$Name,
        [string]$Hostname,
        [int]$ManagementPort,
        [int]$AmqpPort
    )

    $existing = docker ps -a --filter "name=^/$Name$" --format "{{.Names}}"
    if ($existing -eq $Name) {
        Write-Host "El contenedor '$Name' ya existe."
        return
    }

    docker run -d --hostname $Hostname --name $Name `
        -e RABBITMQ_DEFAULT_USER=root -e RABBITMQ_DEFAULT_PASS=root `
        -p "${ManagementPort}:15672" -p "${AmqpPort}:5672" `
        rabbitmq:3-management
}

New-RabbitContainer -Name rabbitA -Hostname rabbitA -ManagementPort 9090 -AmqpPort 5555
New-RabbitContainer -Name rabbitB -Hostname rabbitB -ManagementPort 9191 -AmqpPort 6666

Write-Host "Esperando a que ambos servers arranquen del todo..."
# Si el nodo aun no ha arrancado, el plugin queda habilitado pero NO en ejecucion (/api/shovels devuelve 400).
foreach ($name in @("rabbitA", "rabbitB")) {
    docker exec $name rabbitmqctl await_startup
}

foreach ($name in @("rabbitA", "rabbitB")) {
    docker exec $name rabbitmq-plugins enable rabbitmq_shovel
    docker exec $name rabbitmq-plugins enable rabbitmq_shovel_management
}

$networkName = "rabbit-migration-net"
$networkExists = docker network ls --filter "name=^$networkName$" --format "{{.Name}}"
if ($networkExists -ne $networkName) {
    docker network create $networkName | Out-Null
}

foreach ($name in @("rabbitA", "rabbitB")) {
    $alreadyConnected = docker inspect $name --format "{{json .NetworkSettings.Networks}}" | Select-String $networkName
    if (-not $alreadyConnected) {
        docker network connect $networkName $name
    }
}

Write-Host ""
Write-Host "Listo:"
Write-Host "  RabbitA -> management http://localhost:9090 (user/pass: root/root), AMQP host:puerto localhost:5555"
Write-Host "  RabbitB -> management http://localhost:9191 (user/pass: root/root), AMQP host:puerto localhost:6666"
Write-Host "  Dentro de la red '$networkName', cada broker resuelve al otro por su nombre de contenedor (rabbitA / rabbitB) en el puerto interno 5672."
