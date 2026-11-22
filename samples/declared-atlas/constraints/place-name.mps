<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="b2000000-0000-4000-9003-000000000001" name="place-name">
  <nodes>
    <node id="b2000000-0000-4000-8003-000000000001" concept="Constraint.Rule" name="a place is still called &quot;unnamed&quot;; give it a name.">
      <properties>
        <conceptKey>Atlas.Place</conceptKey>
        <message>a place is still called "unnamed"; give it a name.</message>
        <severity>hint</severity>
        <code>atlas.place.name</code>
      </properties>
      <children role="body">
        <node id="b2000000-0000-4000-8003-000000000101" concept="Constraint.DefaultValue">
          <properties>
            <slot>name</slot>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>