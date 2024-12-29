$RG="iot-perf-aks"
$LOCATION="westeurope"
$CLUSTER="iot-perf-cluster"
$ACR="iotperfregistry"

# Nettoyage
az group delete --name $RG --yes
Start-Sleep -Seconds 120

# Création groupe
az group create --name $RG --location $LOCATION

# Cluster AKS minimal
az aks create `
    --resource-group $RG `
    --name $CLUSTER `
    --node-count 1 `
    --node-vm-size Standard_B2s `
    --network-plugin kubenet `
    --generate-ssh-keys

# Configuration kubectl
Start-Sleep -Seconds 30
az aks get-credentials --resource-group $RG --name $CLUSTER --overwrite-existing
kubectl config use-context "iot-perf-cluster"

# Liaison ACR
az aks update -n $CLUSTER -g $RG --attach-acr $ACR

# Déploiement application
$deploymentYaml = @"
apiVersion: apps/v1
kind: Deployment
metadata:
  name: iothighperf
spec:
  replicas: 1
  selector:
    matchLabels:
      app: iothighperf
  template:
    metadata:
      labels:
        app: iothighperf
    spec:
      containers:
      - name: iothighperf
        image: ${ACR}.azurecr.io/iothighperf:latest
        resources:
          requests:
            cpu: "250m"
            memory: "512Mi"
          limits:
            cpu: "500m"
            memory: "1Gi"
        ports:
        - containerPort: 5000
        - containerPort: 5001
---
apiVersion: v1
kind: Service
metadata:
  name: iothighperf
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 5000
    name: http
  - port: 443
    targetPort: 5001
    name: https
  selector:
    app: iothighperf
"@

Start-Sleep -Seconds 30
$deploymentYaml | kubectl apply -f -

Write-Host "Attente de l'IP externe..."
kubectl get svc iothighperf --watch