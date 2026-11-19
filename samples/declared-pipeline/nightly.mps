<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="d60d5dea-91b3-4cdf-9c1a-2ab30b7c871a" name="nightly">
  <nodes>
    <node id="27130afa-9ccb-4550-b245-8f58942414ad" concept="Pipeline.Pipeline" name="Nightly">
      <properties>
        <name>Nightly</name>
      </properties>
      <children role="stages">
        <node id="b6aab2c7-67cb-44b0-aaed-ba45d21ea712" concept="Pipeline.Stage" name="build">
          <properties>
            <name>build</name>
            <command>dotnet build</command>
            <retries>0</retries>
          </properties>
        </node>
        <node id="08005bec-8bd7-4ebe-896f-538c59deb137" concept="Pipeline.Stage" name="smoke">
          <properties>
            <name>smoke</name>
            <retries>0</retries>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>