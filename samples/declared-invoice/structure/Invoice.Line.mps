<?xml version="1.0" encoding="utf-8"?>
<model formatVersion="1" id="f0000000-0000-4000-9000-000000000002" name="Invoice.Line">
  <nodes>
    <node id="f0000000-0000-4000-8000-000000000002" concept="Structure.Concept" name="Invoice.Line">
      <properties>
        <key>Invoice.Line</key>
        <isAbstract>false</isAbstract>
        <isInterface>false</isInterface>
        <isRoot>true</isRoot>
        <alias>line</alias>
        <isOfferedInCompletion>true</isOfferedInCompletion>
        <description>One line of an invoice: what is billed and how much of it.</description>
        <nameSlot>label</nameSlot>
        <nameRule>camel</nameRule>
      </properties>
      <children role="properties">
        <node id="f0000000-0000-4000-8000-000000000102" concept="Structure.PropertyDecl" name="label">
          <properties>
            <name>label</name>
            <typeName>string</typeName>
            <showDescriptionWhenFilled>false</showDescriptionWhenFilled>
          </properties>
        </node>
      </children>
      <children role="children">
        <node id="f0000000-0000-4000-8000-000000000202" concept="Structure.ChildDecl" name="quantity">
          <properties>
            <name>quantity</name>
            <childConceptKey>Units.Quantity</childConceptKey>
            <cardinality>1</cardinality>
          </properties>
        </node>
      </children>
    </node>
  </nodes>
</model>