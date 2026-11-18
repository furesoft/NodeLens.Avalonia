<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="4cb528b0-22ad-44c6-9157-ca302a24e6bd" name="stage-name">
  <nodes>
    <node id="7cbc6ed0-5a16-46bc-a05b-00d4a042c4bb" concept="Constraint.Rule" name="a stage is still called &quot;unnamed&quot;; give it a name.">
      <properties>
        <conceptKey>Pipeline.Stage</conceptKey>
        <message>a stage is still called "unnamed"; give it a name.</message>
        <severity>hint</severity>
        <code>pipeline.stage.name</code>
      </properties>
      <children role="body">
        <node id="2b3c2805-c1d0-46c3-951d-9c3a21daa49a" concept="Constraint.DefaultValue">
          <properties>
            <slot>name</slot>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>