<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="e7000000-0000-4000-8008-000000000001" name="ci">
  <nodes>
    <node id="e7000000-0000-4000-8008-000000000002" concept="Pipeline.Pipeline" name="ci">
      <properties>
        <name>ci</name>
      </properties>
      <children role="stages">
        <node id="e7000000-0000-4000-8008-000000000003" concept="Pipeline.Stage" name="build">
          <properties>
            <name>build</name>
            <command>dotnet build</command>
            <retries>1</retries>
          </properties>
        </node>
        <node id="e7000000-0000-4000-8008-000000000004" concept="Pipeline.Stage" name="test">
          <properties>
            <name>test</name>
            <command>dotnet test</command>
            <retries>2</retries>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>